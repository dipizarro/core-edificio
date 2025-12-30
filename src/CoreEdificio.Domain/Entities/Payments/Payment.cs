namespace CoreEdificio.Domain.Entities.Payments;

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CommunityId { get; set; }
    public Guid UnitId { get; set; }

    public string Period { get; set; } = null!; // YYYY-MM
    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; } = PaymentMethod.Transfer;
    public string? Reference { get; set; }

    public DateTime PaidAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
