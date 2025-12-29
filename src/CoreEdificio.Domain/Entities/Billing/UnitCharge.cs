namespace CoreEdificio.Domain.Entities.Billing;

public class UnitCharge
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BillingPeriodId { get; set; }
    public BillingPeriod BillingPeriod { get; set; } = null!;

    public Guid UnitId { get; set; }

    public decimal CoefficientPct { get; set; } // snapshot
    public decimal Amount { get; set; }         // resultado prorrateo (2 decimales)
}
