namespace CoreEdificio.Domain.Entities.Billing;

/// <summary>
/// Representa la cuota parte de gasto común asignada a una unidad en un período específico.
/// </summary>
public class UnitCharge
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BillingPeriodId { get; set; }
    public BillingPeriod BillingPeriod { get; set; } = null!;

    public Guid UnitId { get; set; }

    /// <summary>
    /// Coeficiente de participación de la unidad al momento del prorrateo.
    /// </summary>
    public decimal CoefficientPct { get; set; }
    
    /// <summary>
    /// Monto resultante del prorrateo.
    /// </summary>
    public decimal Amount { get; set; }
}
