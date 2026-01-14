namespace CoreEdificio.Domain.Entities;

public class UnitComponent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CommunityId { get; set; }
    
    public Guid UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public string Type { get; set; } = "Department"; // "Department" | "Parking" | "Storage" | "Other"
    public string Code { get; set; } = null!;        // "101", "E-100", "B-92"
    public decimal CoefficientPct { get; set; }      // 1.25 = 1.25%
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
