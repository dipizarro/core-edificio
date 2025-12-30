using CoreEdificio.Application.Interfaces.Billing;

namespace CoreEdificio.Application.Services;

public static class BillingProrationCalculator
{
    public record ComputedCharge(Guid UnitId, string UnitNumber, decimal CoefficientPct, decimal Amount);

    public static List<ComputedCharge> Compute(List<UnitSnapshot> units, decimal totalExpenses)
    {
        var raw = units.Select(u =>
        {
            var amount = totalExpenses * (u.CoefficientPct / 100m);
            var rounded = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
            return new ComputedCharge(u.UnitId, u.Number, u.CoefficientPct, rounded);
        }).ToList();

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
}
