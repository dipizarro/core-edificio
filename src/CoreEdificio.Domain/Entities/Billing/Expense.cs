namespace CoreEdificio.Domain.Entities.Billing;

/// <summary>
/// Representa un gasto incurrido por la comunidad en un período determinado.
/// </summary>
public class Expense
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CommunityId { get; set; }
    
    /// <summary>
    /// Período en formato "YYYY-MM".
    /// </summary>
    public string Period { get; set; } = null!;

    public string Description { get; set; } = null!;
    public decimal Amount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
