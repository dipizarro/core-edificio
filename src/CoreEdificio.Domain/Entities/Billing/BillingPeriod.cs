namespace CoreEdificio.Domain.Entities.Billing;

/// <summary>
/// Representa un período de facturación mensual para una comunidad.
/// </summary>
public class BillingPeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CommunityId { get; set; }
    
    /// <summary>
    /// Período en formato "YYYY-MM" (ej. 2023-10).
    /// </summary>
    public string Period { get; set; } = null!;

    public BillingPeriodStatus Status { get; set; } = BillingPeriodStatus.Draft;

    public decimal TotalExpenses { get; set; }
    public decimal TotalCoefficientPct { get; set; }

    public DateTime? IssuedAtUtc { get; set; }

    public List<UnitCharge> UnitCharges { get; set; } = [];
}
