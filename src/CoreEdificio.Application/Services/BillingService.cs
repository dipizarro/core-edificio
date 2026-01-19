using CoreEdificio.Application.Common;
using CoreEdificio.Application.Contracts.Billing;
using CoreEdificio.Application.Contracts.Bulk;
using CoreEdificio.Application.Contracts.Billing.Bulk;
using CoreEdificio.Application.Contracts.Billing.Statement;
using CoreEdificio.Application.Interfaces.Billing;
using CoreEdificio.Application.Interfaces.Payments;
using CoreEdificio.Domain.Entities.Billing;
using Microsoft.Extensions.Configuration;

namespace CoreEdificio.Application.Services;

public class BillingService
{
    private const decimal CoefTarget = 100.00m;

    private readonly IExpenseRepository _expenses;
    private readonly IBillingPeriodRepository _periods;
    private readonly IUnitChargeRepository _charges;
    private readonly IUnitReadRepository _units;
    private readonly IPaymentRepository _payments;
    private readonly IUnitOfWork _uow;
    private readonly IConfiguration _config;

    public BillingService(
        IExpenseRepository expenses,
        IBillingPeriodRepository periods,
        IUnitChargeRepository charges,
        IUnitReadRepository units,
        IPaymentRepository payments,
        IUnitOfWork uow,
        IConfiguration config)
    {
        _expenses = expenses;
        _periods = periods;
        _charges = charges;
        _units = units;
        _payments = payments;
        _uow = uow;
        _config = config;
    }

    public async Task<Expense> CreateExpenseAsync(Guid communityId, CreateExpenseCommand cmd, CancellationToken ct = default)
    {
        var period = NormalizePeriod(cmd.Period);
        if (string.IsNullOrWhiteSpace(cmd.Description)) throw new ValidationException("Description is required.");
        if (cmd.Amount <= 0) throw new ValidationException("Amount must be > 0.");

        // Si el período ya está emitido, no se aceptan más gastos
        var existingPeriod = await _periods.GetByCommunityAndPeriodAsync(communityId, period, ct);
        if (existingPeriod?.Status == BillingPeriodStatus.Issued)
            throw new ConflictException("Period is already issued. You can't add expenses.");

        var expense = new Expense
        {
            CommunityId = communityId,
            Period = period,
            Description = cmd.Description.Trim(),
            Amount = decimal.Round(cmd.Amount, 2, MidpointRounding.AwayFromZero),
            CreatedAtUtc = DateTime.UtcNow
        };

        await _expenses.AddAsync(expense, ct);
        return expense;
    }

    public async Task<BillingSummaryDto> IssueAsync(Guid communityId, IssueBillingPeriodCommand cmd, CancellationToken ct = default)
    {
        var period = NormalizePeriod(cmd.Period);

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            // 1) Evitar doble emisión
            var existing = await _periods.GetByCommunityAndPeriodAsync(communityId, period, token);
            if (existing is not null && existing.Status == BillingPeriodStatus.Issued)
                throw new ConflictException("Period already issued.");

            // Si el BillingPeriod existe (Draft) y ya hay cargos, bloqueamos para evitar duplicados.
            if (existing is not null && existing.Status == BillingPeriodStatus.Draft)
            {
                var hasCharges = await _charges.AnyByBillingPeriodIdAsync(existing.Id, token);
                if (hasCharges)
                    throw new ConflictException("Charges already generated for this period.");
            }

            // 2) Leer unidades (snapshot)
            var units = await _units.ListSnapshotsByCommunityAsync(communityId, token);
            if (units.Count == 0) throw new ValidationException("No units found for this community.");

            // 3) Total coeficientes
            var totalCoef = units.Sum(x => x.CoefficientPct);
            if (totalCoef <= 0) throw new ValidationException("Total coefficient sum cannot be zero. Ensure units have valid components or coefficients.");

            // (en v0.x aceptamos tolerancia; más adelante podemos exigir 100 exacto)
            if (decimal.Abs(totalCoef - CoefTarget) > 0.01m)
                throw new ValidationException($"Total coefficient must be {CoefTarget} (tolerance 0.01). Current: {totalCoef:0.####}");

            // 4) Total gastos
            var totalExpenses = await _expenses.GetTotalByCommunityAndPeriodAsync(communityId, period, token);
            if (totalExpenses <= 0) throw new ValidationException("No expenses found for this period.");

            // 5) Crear/actualizar BillingPeriod en Draft (si no existe)
            var billing = existing ?? new BillingPeriod
            {
                CommunityId = communityId,
                Period = period,
                Status = BillingPeriodStatus.Draft
            };

            billing.TotalExpenses = decimal.Round(totalExpenses, 2, MidpointRounding.AwayFromZero);
            billing.TotalCoefficientPct = totalCoef;

            if (existing is null) await _periods.AddAsync(billing, token);
            else await _periods.UpdateAsync(billing, token);

            // 6) Prorrateo + redondeo
            //var computed = ComputeCharges(units, billing.TotalExpenses);
            var computed = BillingProrationCalculator.Compute(units, billing.TotalExpenses);

            // 7) Persistir UnitCharges
            var entities = computed.Select(x => new UnitCharge
            {
                BillingPeriodId = billing.Id,
                UnitId = x.UnitId,
                CoefficientPct = x.CoefficientPct,
                Amount = x.Amount
            }).ToList();

            await _charges.AddRangeAsync(entities, token);

            // 8) Marcar como Issued
            billing.Status = BillingPeriodStatus.Issued;
            billing.IssuedAtUtc = DateTime.UtcNow;
            await _periods.UpdateAsync(billing, token);
        }, ct);

        // 9) Devolver resumen (lo recalculamos desde repos en Paso 3, por ahora simple)
        // Para no agregar más queries acá, en Paso 3 hacemos GetSummary real por repos.
        // Igual devolvemos el "cálculo" determinístico.
        var unitsForResponse = await _units.ListSnapshotsByCommunityAsync(communityId, ct);
        var total = await _expenses.GetTotalByCommunityAndPeriodAsync(communityId, NormalizePeriod(cmd.Period), ct);
        //var charges = ComputeCharges(unitsForResponse, decimal.Round(total, 2, MidpointRounding.AwayFromZero));
        var charges = BillingProrationCalculator.Compute(unitsForResponse, total);

        return new BillingSummaryDto(
            communityId,
            NormalizePeriod(cmd.Period),
            decimal.Round(total, 2, MidpointRounding.AwayFromZero),
            unitsForResponse.Sum(x => x.CoefficientPct),
            unitsForResponse.Count,
            charges.Select(c => new UnitChargeDto(c.UnitId, c.UnitNumber, c.CoefficientPct, c.Amount)).ToList()
        );
    }

    private static string NormalizePeriod(string period)
    {
        if (string.IsNullOrWhiteSpace(period)) throw new ValidationException("Period is required (YYYY-MM).");
        period = period.Trim();
        // Validación mínima
        if (period.Length != 7 || period[4] != '-') throw new ValidationException("Period must be in format YYYY-MM.");
        return period;
    }

    private sealed record ComputedCharge(Guid UnitId, string UnitNumber, decimal CoefficientPct, decimal Amount);

    private static List<ComputedCharge> ComputeCharges(List<UnitSnapshot> units, decimal totalExpenses)
    {
        // calculo sin redondeo final
        var raw = units.Select(u =>
        {
            var amount = totalExpenses * (u.CoefficientPct / 100m);
            var rounded = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
            return new ComputedCharge(u.UnitId, u.Number, u.CoefficientPct, rounded);
        }).ToList();

        // Ajuste por diferencia de redondeo para que sume exacto al total
        var sumRounded = raw.Sum(x => x.Amount);
        var diff = decimal.Round(totalExpenses - sumRounded, 2, MidpointRounding.AwayFromZero);

        if (diff != 0)
        {
            var idx = raw
                .Select((x, i) => new { x, i })
                .OrderByDescending(t => t.x.CoefficientPct)
                .ThenBy(t => t.x.UnitNumber)
                .First().i;

            raw[idx] = raw[idx] with { Amount = raw[idx].Amount + diff };
        }

        return raw;
    }

    public async Task<BillingSummaryDto> GetSummaryAsync(Guid communityId, string period, CancellationToken ct = default)
    {
        period = NormalizePeriod(period);

        var summary = await _periods.GetSummaryAsync(communityId, period, ct);
        if (summary is null) throw new NotFoundException("Billing period not found.");

        return summary;
    }

    public async Task<BulkResponse<Expense>> CreateExpensesBulkAsync(Guid communityId, CreateExpensesBulkCommand bulk, CancellationToken ct = default)
    {
        var results = new List<BulkItemResult<Expense>>();
        var expensesToCreate = new List<Expense>();

        if (bulk.Expenses is null || bulk.Expenses.Count == 0)
        {
             return new BulkResponse<Expense>(communityId, 0, 0, 0, results);
        }

        // Cachear estados de periodos para no consultar por cada item
        // Asumimos que todos los items pueden tener periodos distintos.
        // Pero optimización: buscar distinct periods y validar status.
        var distinctPeriods = bulk.Expenses
            .Select(x => x.Period)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct()
            .Select(NormalizePeriod) // Cuidado: esto puede tirar excepcion si formato invalido. Mejor validar dentro del loop.
            .ToList(); 
        
        // No, mejor validamos uno por uno en el loop o hacemos un pre-pass seguro.
        // Haremos check one-by-one pero optimizado con diccionario local si se repiten.
        var periodStatusCache = new Dictionary<string, bool>(); // Period -> IsIssued (true=bloqueado)

        foreach (var (cmd, index) in bulk.Expenses.Select((c, i) => (c, i)))
        {
            try
            {
                var period = NormalizePeriod(cmd.Period); // throws ValidationException
                
                if (string.IsNullOrWhiteSpace(cmd.Description)) 
                {
                    results.Add(new BulkItemResult<Expense>(index, false, "Description is required", null));
                    continue;
                }
                if (cmd.Amount <= 0)
                {
                    results.Add(new BulkItemResult<Expense>(index, false, "Amount must be > 0", null));
                    continue;
                }

                // Check Period Status
                if (!periodStatusCache.ContainsKey(period))
                {
                    var isIssued = await _expenses.AnyForIssuedPeriodAsync(communityId, period, ct);
                    periodStatusCache[period] = isIssued;
                }

                if (periodStatusCache[period])
                {
                    results.Add(new BulkItemResult<Expense>(index, false, "Period is already issued", null));
                    continue;
                }

                // Exito
                var expense = new Expense
                {
                    CommunityId = communityId,
                    Period = period,
                    Description = cmd.Description.Trim(),
                    Amount = decimal.Round(cmd.Amount, 2, MidpointRounding.AwayFromZero),
                    CreatedAtUtc = DateTime.UtcNow
                };

                expensesToCreate.Add(expense);
                results.Add(new BulkItemResult<Expense>(index, true, null, expense));

            }
            catch (ValidationException ex)
            {
                results.Add(new BulkItemResult<Expense>(index, false, ex.Message, null));
            }
            catch (Exception ex)
            {
                 results.Add(new BulkItemResult<Expense>(index, false, "Internal error: " + ex.Message, null));
            }
        }

        if (expensesToCreate.Count > 0)
        {
            await _expenses.AddRangeAsync(expensesToCreate, ct);
        }

        var createdCount = results.Count(x => x.Success);
        var failedCount = results.Count(x => !x.Success);

        return new BulkResponse<Expense>(communityId, bulk.Expenses.Count, createdCount, failedCount, results);
    }

    public async Task<UnitStatementDto> GetUnitStatementAsync(Guid communityId, Guid unitId, string period, CancellationToken ct)
    {
        period = NormalizePeriod(period);

        // 1. Validar comunidad y obtener unidad
        // Usamos ListSnapshotsByCommunityAsync porque IUnitReadRepository no tiene GetById
        var units = await _units.ListSnapshotsByCommunityAsync(communityId, ct);
        var unit = units.FirstOrDefault(u => u.UnitId == unitId);
        
        if (unit is null)
            throw new NotFoundException($"Unit {unitId} not found in community {communityId}.");

        // 2. Saldo Anterior (Previous Balance)
        var chargesBefore = await _charges.GetChargesBeforePeriodAsync(communityId, unitId, period, ct);
        var paymentsBefore = await _payments.GetPaymentsBeforePeriodAsync(communityId, unitId, period, ct);

        var chargesBeforeTotal = chargesBefore.Sum(x => x.Amount);
        var paymentsBeforeTotal = paymentsBefore.Sum(x => x.Amount);
        
        var previousBalance = chargesBeforeTotal - paymentsBeforeTotal;

        // 3. Movimientos del Periodo (Current Charges & Payments)
        var currentCharges = await _charges.GetChargesForPeriodAsync(communityId, unitId, period, ct);
        var currentPayments = await _payments.GetPaymentsForPeriodAsync(communityId, unitId, period, ct);

        var currentChargesTotal = currentCharges.Sum(x => x.Amount);
        var paymentsTotal = currentPayments.Sum(x => x.Amount);

        // 4. Total a Pagar
        var totalDue = previousBalance + currentChargesTotal - paymentsTotal;

        // 5. Construir Líneas
        var lines = new List<StatementLineDto>();

        // Agregamos cargos
        lines.AddRange(currentCharges.Select(c => new StatementLineDto(
            Type: "Charge", 
            Description: "Gasto Común", 
            Amount: c.Amount, 
            Date: c.BillingPeriod.IssuedAtUtc ?? DateTime.MinValue,
            Period: period
        )));

        // Agregamos pagos
        lines.AddRange(currentPayments.Select(p => new StatementLineDto(
            Type: "Payment",
            Description: "Abono",
            Amount: p.Amount, // Pagos restan a la deuda, pero aquí mostramos monto absoluto? 
            // En el statement suele mostrarse positivo en la columna de abonos. 
            // El DTO Lines tiene Amount. Lo dejaremos positivo y el Type indica si suma o resta.
            Date: p.PaidAtUtc,
            Period: p.Period
        )));

        // Ordenar por fecha
        lines = lines.OrderBy(x => x.Date).ToList();

        // 6. Calcular DueDate
        var dueDay = _config.GetValue<int>("Billing:DueDayOfMonth", 0);
        
        // Asumimos vencimiento en el mes SIGUIENTE al periodo.
        // Ejemplo: Periodo 2024-01-01 -> Vencimiento Feb.
        var parts = period.Split('-');
        var year = int.Parse(parts[0]);
        var month = int.Parse(parts[1]);
        var periodDate = new DateTime(year, month, 1);
        var nextMonth = periodDate.AddMonths(1);

        DateTime dueDate;
        if (dueDay > 0)
        {
             var daysInNextMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
             var day = Math.Min(dueDay, daysInNextMonth);
             dueDate = new DateTime(nextMonth.Year, nextMonth.Month, day);
        }
        else
        {
             // Default: día 10 del mes siguiente si no hay config
             // O último día del mes siguiente? El usuario dijo "o último día del mes si no existe" refiriéndose al nro de día.
             // Asumiremos día 10 por defecto si no hay config.
             dueDay = 10;
             var daysInNextMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
             var day = Math.Min(dueDay, daysInNextMonth);
             dueDate = new DateTime(nextMonth.Year, nextMonth.Month, day);
        }

        return new UnitStatementDto(
            CommunityId: communityId,
            UnitId: unitId,
            UnitNumber: unit.Number,
            Period: period,
            PreviousBalance: previousBalance,
            CurrentChargesTotal: currentChargesTotal,
            PaymentsTotal: paymentsTotal,
            TotalDue: totalDue,
            DueDate: dueDate,
            UnitTotalCoefficientPct: unit.CoefficientPct,
            Components: unit.Components.Select(c => new UnitComponentDto(c.Type, c.Code, c.CoefficientPct, c.IsActive)).ToList(),
            Lines: lines
        );
    }

}
