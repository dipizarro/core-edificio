namespace CoreEdificio.Domain.Entities.Billing;

/// <summary>
/// Representa un cobro individual a una unidad, independientemente del gasto común base.
/// </summary>
public class Charge
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CommunityId { get; set; }
    public Guid UnitId { get; set; }
    
    public decimal Amount { get; set; }
    public string Description { get; set; } = null!;
    
    /// <summary>
    /// Período de facturación asignado en formato "YYYY-MM".
    /// </summary>
    public string Period { get; set; } = null!;

    /// <summary>
    /// Tipo de origen del cobro (ej. "FacilityBooking").
    /// </summary>
    public string SourceType { get; set; } = null!;
    public Guid? SourceId { get; set; }
    public string? SourceRef { get; set; }
    
    /// <summary>
    /// Clase de cobro ("Rent", "Deposit", "Fine").
    /// </summary>
    public string ChargeKind { get; set; } = null!;
    
    /// <summary>
    /// Clasificación de la multa ("LateCancel", "NoShow").
    /// </summary>
    public string? FineType { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
