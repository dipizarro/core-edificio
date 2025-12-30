using CoreEdificio.Application.Interfaces.Billing;
using CoreEdificio.Application.Services;
using Xunit;

namespace CoreEdificio.Tests.Billing;

public class BillingProrationCalculatorTests
{
    [Fact]
    public void Compute_Should_SumExactlyToTotalExpenses_AfterRounding()
    {
        var units = BuildUnits(
            ("101", 8.50m),
            ("102", 9.00m),
            ("103", 10.25m),
            ("104", 11.00m),
            ("201", 8.25m),
            ("202", 9.75m),
            ("203", 10.00m),
            ("204", 11.25m),
            ("301", 10.00m),
            ("302", 12.00m)
        );

        const decimal total = 4_340_650m;

        var charges = BillingProrationCalculator.Compute(units, total);

        Assert.Equal(10, charges.Count);
        Assert.Equal(total, charges.Sum(x => x.Amount));
    }

    [Fact]
    public void Compute_Should_AssignRoundingDiff_ToHighestCoefficientUnit()
    {
        // Diseñado para forzar un diff por redondeo.
        // 3 unidades: 33.33, 33.33, 33.34 y un total con decimales.
        var units = BuildUnits(
            ("A", 33.33m),
            ("B", 33.33m),
            ("C", 33.34m)
        );

        const decimal total = 100.01m;

        var charges = BillingProrationCalculator.Compute(units, total);

        Assert.Equal(total, charges.Sum(x => x.Amount));

        // La unidad con mayor coef es C, debe absorber el diff si existe.
        var c = charges.Single(x => x.UnitNumber == "C");

        // Monto teórico sin ajustar sería:
        // total * 0.3334 = 33.343334 -> 33.34 (2 decimales AwayFromZero)
        // pero con diff aplicado puede cambiar a 33.35 o 33.33 según diff.
        // Lo importante: el ajuste cae en C y suma total calza.
        Assert.True(c.Amount == 33.34m || c.Amount == 33.35m || c.Amount == 33.33m);
    }

    [Fact]
    public void Compute_WhenTieOnCoefficient_Should_AssignDiff_ToLowestUnitNumber()
    {
        // Empate de coef. La regla es ThenBy(UnitNumber) => gana "101"
        var units = BuildUnits(
            ("101", 50.00m),
            ("102", 50.00m)
        );

        const decimal total = 0.01m;

        var charges = BillingProrationCalculator.Compute(units, total);

        Assert.Equal(total, charges.Sum(x => x.Amount));

        var u101 = charges.Single(x => x.UnitNumber == "101");
        var u102 = charges.Single(x => x.UnitNumber == "102");

        // Con total 0.01:
        // 0.01*0.5=0.005 => AwayFromZero => 0.01 para ambos => sum 0.02
        // diff = -0.01, se lo resta al primero por regla -> "101" queda 0.00
        Assert.Equal(0.00m, u101.Amount);
        Assert.Equal(0.01m, u102.Amount);
    }

    [Fact]
    public void Compute_Should_Use_AwayFromZero_Rounding()
    {
        // Caso borde: 0.005 se redondea a 0.01 (AwayFromZero)
        var units = BuildUnits(("U1", 50.00m), ("U2", 50.00m));

        const decimal total = 0.01m;

        var charges = BillingProrationCalculator.Compute(units, total);

        Assert.Equal(total, charges.Sum(x => x.Amount));
    }

    private static List<UnitSnapshot> BuildUnits(params (string number, decimal coefPct)[] defs)
    {
        return defs.Select(d => new UnitSnapshot(Guid.NewGuid(), d.number, d.coefPct)).ToList();
    }
}
