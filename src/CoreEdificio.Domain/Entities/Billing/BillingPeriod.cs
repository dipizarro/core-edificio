namespace CoreEdificio.Domain.Entities.Billing;

public class BillingPeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CommunityId { get; set; }
    public string Period { get; set; } = null!; // "YYYY-MM"

    public BillingPeriodStatus Status { get; set; } = BillingPeriodStatus.Draft;

    public decimal TotalExpenses { get; set; }
    public decimal TotalCoefficientPct { get; set; }

    public DateTime? IssuedAtUtc { get; set; }

    public List<UnitCharge> UnitCharges { get; set; } = new();
}
