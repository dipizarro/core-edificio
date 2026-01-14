namespace CoreEdificio.Domain.Entities;

public class Unit
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CommunityId { get; set; }
    public Community Community { get; set; } = null!;

    public string Number { get; set; } = null!; // "101", "1203", "B-14"
    public decimal CoefficientPct { get; set; } // 1.25 = 1.25%
    public string? OwnerName { get; set; }
    public string? OwnerEmail { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<UserUnit> UserUnits { get; set; } = new List<UserUnit>();
    public ICollection<UnitComponent> Components { get; set; } = new List<UnitComponent>();

    public decimal GetTotalCoefficientPct()
    {
        if (Components == null || !Components.Any(x => x.IsActive))
        {
            return CoefficientPct;
        }

        return Components.Where(x => x.IsActive).Sum(x => x.CoefficientPct);
    }
}
