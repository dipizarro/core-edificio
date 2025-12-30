using CoreEdificio.Application.Common;
using CoreEdificio.Application.Contracts.Billing;
using CoreEdificio.Application.Interfaces.Billing;
using CoreEdificio.Domain.Entities.Billing;

namespace CoreEdificio.Application.Services;

public class BillingService
{
    private const decimal CoefTarget = 100.00m;

    private readonly IExpenseRepository _expenses;
    private readonly IBillingPeriodRepository _periods;
    private readonly IUnitChargeRepository _charges;
    private readonly IUnitReadRepository _units;
    private readonly IUnitOfWork _uow;

    public BillingService(
        IExpenseRepository expenses,
        IBillingPeriodRepository periods,
        IUnitChargeRepository charges,
        IUnitReadRepository units,
        IUnitOfWork uow)
    {
        _expenses = expenses;
        _periods = periods;
        _charges = charges;
        _units = units;
        _uow = uow;
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

            // 2) Leer unidades (snapshot)
            var units = await _units.ListSnapshotsByCommunityAsync(communityId, token);
            if (units.Count == 0) throw new ValidationException("No units found for this community.");

            // 3) Total coeficientes
            var totalCoef = units.Sum(x => x.CoefficientPct);
            if (totalCoef <= 0) throw new ValidationException("Total coefficient must be > 0.");

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

}
