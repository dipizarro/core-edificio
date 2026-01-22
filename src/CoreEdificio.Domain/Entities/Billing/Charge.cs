namespace CoreEdificio.Domain.Entities.Billing;

public class Charge
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CommunityId { get; set; }
    public Guid UnitId { get; set; }
    
    public decimal Amount { get; set; }
    public string Description { get; set; } = null!;
    public string Period { get; set; } = null!; // "YYYY-MM"

    public string SourceType { get; set; } = null!; // "FacilityBooking"
    public Guid? SourceId { get; set; }
    public string? SourceRef { get; set; }
    public string ChargeKind { get; set; } = null!; // "Rent" | "Deposit" | "Fine"
    public string? FineType { get; set; } // "LateCancel" | "NoShow"

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
