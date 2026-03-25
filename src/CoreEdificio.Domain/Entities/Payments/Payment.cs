namespace CoreEdificio.Domain.Entities.Payments;

/// <summary>
/// Representa un pago registrado en el sistema.
/// </summary>
public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CommunityId { get; set; }
    public Guid UnitId { get; set; }

    /// <summary>
    /// Período imputado (ej. "YYYY-MM").
    /// </summary>
    public string Period { get; set; } = null!;
    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; } = PaymentMethod.Transfer;
    public string? Reference { get; set; }

    public DateTime PaidAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
