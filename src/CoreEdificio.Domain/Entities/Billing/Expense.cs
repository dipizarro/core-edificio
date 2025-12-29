namespace CoreEdificio.Domain.Entities.Billing;

public class Expense
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CommunityId { get; set; }
    public string Period { get; set; } = null!; // "YYYY-MM"

    public string Description { get; set; } = null!;
    public decimal Amount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
