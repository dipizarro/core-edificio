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

/// <summary>
/// Servicio responsable de consolidar y emitir el Gasto Común (Facturación) y de gestionar cobros/multas y estados de cuenta.
/// </summary>
public class BillingService
{
    private const decimal CoefTarget = 100.00m;

    private readonly IExpenseRepository _expenses;
    private readonly IBillingPeriodRepository _periods;
    private readonly IUnitChargeRepository _charges;
    private readonly IChargeRepository _manualCharges;
    private readonly IUnitReadRepository _units;
    private readonly IPaymentRepository _payments;
    private readonly IUnitOfWork _uow;
    private readonly IConfiguration _config;

    public BillingService(
        IExpenseRepository expenses,
        IBillingPeriodRepository periods,
        IUnitChargeRepository charges,
        IChargeRepository manualCharges,
        IUnitReadRepository units,
        IPaymentRepository payments,
        IUnitOfWork uow,
        IConfiguration config)
    {
        _expenses = expenses;
        _periods = periods;
        _charges = charges;
        _manualCharges = manualCharges;
        _units = units;
        _payments = payments;
        _uow = uow;
        _config = config;
    }

    /// <summary>
    /// Crea un gasto individual para un periodo específico, asegurando que el periodo no esté cerrado.
    /// </summary>
    public async Task<Expense> CreateExpenseAsync(Guid communityId, CreateExpenseCommand cmd, CancellationToken ct = default)
    {
        var period = NormalizePeriod(cmd.Period);
        if (string.IsNullOrWhiteSpace(cmd.Description)) throw new ValidationException("La descripción es obligatoria.");
        if (cmd.Amount <= 0) throw new ValidationException("El monto debe ser numérico mayor a cero.");

        // Si el período ya está emitido, no se aceptan más gastos
        var existingPeriod = await _periods.GetByCommunityAndPeriodAsync(communityId, period, ct);
        if (existingPeriod?.Status == BillingPeriodStatus.Issued)
            throw new ConflictException("El periodo ya se encuentra emitido. No puede añadir más gastos.");

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

    /// <summary>
    /// Emite el gasto común para un periodo, prorrateando los gastos totales contra los porcentajes de unidades.
    /// Esta operación es transaccional debido a su impacto contable.
    /// </summary>
    public async Task<BillingSummaryDto> IssueAsync(Guid communityId, IssueBillingPeriodCommand cmd, CancellationToken ct = default)
    {
        var period = NormalizePeriod(cmd.Period);

        // Lógica Transaccional: 
        // Generar un cobro masivo bloquea registros y no debe interrumpirse a la mitad (ACID).
        // Se valida además que los coeficientes de las unidades sumen cercano al 100%.
        await _uow.ExecuteInTransactionAsync(async token =>
        {
            // 1) Evitar doble emisión
            var existing = await _periods.GetByCommunityAndPeriodAsync(communityId, period, token);
            if (existing is not null && existing.Status == BillingPeriodStatus.Issued)
                throw new ConflictException("El periodo ya fue emitido.");

            // Si el BillingPeriod existe (Draft) y ya hay cargos, bloqueamos para evitar duplicados.
            if (existing is not null && existing.Status == BillingPeriodStatus.Draft)
            {
                var hasCharges = await _charges.AnyByBillingPeriodIdAsync(existing.Id, token);
                if (hasCharges)
                    throw new ConflictException("Los cargos de prorrateo ya han sido generados localmente.");
            }

            // 2) Leer unidades (snapshot)
            var units = await _units.ListSnapshotsByCommunityAsync(communityId, token);
            if (units.Count == 0) throw new ValidationException("No se encontraron unidades en esta comunidad para calcular prorrateo.");

            // 3) Total coeficientes
            var totalCoef = units.Sum(x => x.CoefficientPct);
            if (totalCoef <= 0) throw new ValidationException("La suma de coeficientes es cero. Asegúrese de que las unidades posean un valor válido de coeficiente.");

            // Tolerancia temporal de (0.01) porque a veces el prorrateo decimal no da exacto 100%.
            if (decimal.Abs(totalCoef - CoefTarget) > 0.01m)
                throw new ValidationException($"El coeficiente total sumado debe ser {CoefTarget} (margen de error 0.01). Actual: {totalCoef:0.####}");

            // 4) Total gastos
            var totalExpenses = await _expenses.GetTotalByCommunityAndPeriodAsync(communityId, period, token);
            if (totalExpenses <= 0) throw new ValidationException("No existen gastos registrados en este periodo para facturar.");

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

            // 6) Prorrateo delegando a calculador especializado
            var computed = BillingProrationCalculator.Compute(units, billing.TotalExpenses);

            // 7) Persistir UnitCharges resultantes
            var entities = computed.Select(x => new UnitCharge
            {
                BillingPeriodId = billing.Id,
                UnitId = x.UnitId,
                CoefficientPct = x.CoefficientPct,
                Amount = x.Amount
            }).ToList();

            await _charges.AddRangeAsync(entities, token);

            // 8) Marcar como Emitido
            billing.Status = BillingPeriodStatus.Issued;
            billing.IssuedAtUtc = DateTime.UtcNow;
            await _periods.UpdateAsync(billing, token);
        }, ct);

        // Retornamos el contrato final que lee datos ya asentados.
        var unitsForResponse = await _units.ListSnapshotsByCommunityAsync(communityId, ct);
        var total = await _expenses.GetTotalByCommunityAndPeriodAsync(communityId, NormalizePeriod(cmd.Period), ct);
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
        if (string.IsNullOrWhiteSpace(period)) throw new ValidationException("Se requiere un periodo válido (YYYY-MM).");
        period = period.Trim();
        if (period.Length != 7 || period[4] != '-') throw new ValidationException("El periodo debe seguir el formato Año y Mes separado por guion: YYYY-MM.");
        return period;
    }

    /// <summary>
    /// Obtiene el resumen consolidado de un periodo de facturación.
    /// </summary>
    public async Task<BillingSummaryDto> GetSummaryAsync(Guid communityId, string period, CancellationToken ct = default)
    {
        period = NormalizePeriod(period);

        var summary = await _periods.GetSummaryAsync(communityId, period, ct);
        if (summary is null) throw new NotFoundException("El periodo solicitado no existe.");

        return summary;
    }

    /// <summary>
    /// Crea gastos comunes de forma masiva (Bulk) con verificaciones preventivas y sin transaccionamiento drástico
    /// asegurando que la aserción global de 'Issued' bloquee la insersión prematuramente.
    /// </summary>
    public async Task<BulkResponse<Expense>> CreateExpensesBulkAsync(Guid communityId, CreateExpensesBulkCommand bulk, CancellationToken ct = default)
    {
        var results = new List<BulkItemResult<Expense>>();
        var expensesToCreate = new List<Expense>();

        if (bulk.Expenses is null || bulk.Expenses.Count == 0)
        {
             return new BulkResponse<Expense>(communityId, 0, 0, 0, results);
        }

        // Caché local para optimizar queries a BD en caso de inserciones repetitivas del mismo periodo.
        var periodStatusCache = new Dictionary<string, bool>(); // Period -> IsIssued (true=bloqueado)

        foreach (var (cmd, index) in bulk.Expenses.Select((c, i) => (c, i)))
        {
            try
            {
                var period = NormalizePeriod(cmd.Period); 
                
                if (string.IsNullOrWhiteSpace(cmd.Description)) 
                {
                    results.Add(new BulkItemResult<Expense>(index, false, "La descripción es requerida", null));
                    continue;
                }
                if (cmd.Amount <= 0)
                {
                    results.Add(new BulkItemResult<Expense>(index, false, "El monto debe ser numérico mayor a 0", null));
                    continue;
                }

                // Verificación de estado de Periodo
                if (!periodStatusCache.ContainsKey(period))
                {
                    var isIssued = await _expenses.AnyForIssuedPeriodAsync(communityId, period, ct);
                    periodStatusCache[period] = isIssued;
                }

                if (periodStatusCache[period])
                {
                    results.Add(new BulkItemResult<Expense>(index, false, "El periodo se encuentra cerrado/emitido", null));
                    continue;
                }

                // Generación
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
                 // Nota de Seguridad: Evitamos fugas de traza completa exponiendo solo el Message base para Bulk Logs
                 results.Add(new BulkItemResult<Expense>(index, false, "Fallo interno procesando este elemento: " + ex.Message, null));
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

    /// <summary>
    /// Consolida el estado de cuenta y facturación mensual particular para una Unidad específica.
    /// Recupera información de saldos previos, sumas actuales y abonos registrados calculando matemáticamente
    /// la deuda total vencida y facturada (Gasto Común mensual integral).
    /// </summary>
    public async Task<UnitStatementDto> GetUnitStatementAsync(Guid communityId, Guid unitId, string period, CancellationToken ct)
    {
        period = NormalizePeriod(period);

        // 1. Validar comunidad y obtener unidad
        var units = await _units.ListSnapshotsByCommunityAsync(communityId, ct);
        var unit = units.FirstOrDefault(u => u.UnitId == unitId) 
                   ?? throw new NotFoundException($"La unidad no se encuentra en la comunidad {communityId}.");

        // 2. Saldo Anterior (Suma algebraica de cargos previos - pagos previos)
        var chargesBefore = await _charges.GetChargesBeforePeriodAsync(communityId, unitId, period, ct);
        var manualChargesBefore = await _manualCharges.GetBeforePeriodAsync(communityId, unitId, period, ct);
        var paymentsBefore = await _payments.GetPaymentsBeforePeriodAsync(communityId, unitId, period, ct);

        var chargesBeforeTotal = chargesBefore.Sum(x => x.Amount) + manualChargesBefore.Sum(x => x.Amount);
        var paymentsBeforeTotal = paymentsBefore.Sum(x => x.Amount);
        var previousBalance = chargesBeforeTotal - paymentsBeforeTotal;

        // 3. Movimientos del Periodo (Suma actual - pagos mes en curso)
        var currentCharges = await _charges.GetChargesForPeriodAsync(communityId, unitId, period, ct);
        var manualCharges = await _manualCharges.GetForUnitAndPeriodAsync(communityId, unitId, period, ct);
        var currentPayments = await _payments.GetPaymentsForPeriodAsync(communityId, unitId, period, ct);

        var currentChargesTotal = currentCharges.Sum(x => x.Amount) + manualCharges.Sum(x => x.Amount);
        var paymentsTotal = currentPayments.Sum(x => x.Amount);

        // 4. Deuda Integral Resultante
        var totalDue = previousBalance + currentChargesTotal - paymentsTotal;

        // 5. Construir detalle transaccional (Extracto)
        var lines = new List<StatementLineDto>();

        // Agregamos cargos por coeficiente (Gasto Común)
        lines.AddRange(currentCharges.Select(c => new StatementLineDto(
            Type: "Charge", 
            Description: "Gasto Común", 
            Amount: c.Amount, 
            Date: c.BillingPeriod.IssuedAtUtc ?? DateTime.MinValue,
            Period: period
        )));

        // Agregamos cargos manuales (Consumos, Reservas, Multas)
        lines.AddRange(manualCharges.Select(c => new StatementLineDto(
            Type: "Charge",
            Description: c.Description,
            Amount: c.Amount,
            Date: c.CreatedAtUtc,
            Period: period
        )));

        // Agregamos pagos o abonos (Deposit/Transfer)
        lines.AddRange(currentPayments.Select(p => new StatementLineDto(
            Type: "Payment",
            Description: "Abono / Pago Gasto Común",
            Amount: p.Amount, 
            Date: p.PaidAtUtc,
            Period: p.Period
        )));

        // Estructura ordenada cronológicamente
        lines = lines.OrderBy(x => x.Date).ToList();

        // 6. Fechas de Expiración
        var dueDay = _config.GetValue<int>("Billing:DueDayOfMonth", 0);
        
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
             // Por defecto se toma el día 10 si no ha sido explícitamente parametrizado.
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
