namespace CoreEdificio.Domain.Entities;

/// <summary>
/// Representa una unidad independiente dentro de una comunidad (ej. Departamento, Estacionamiento, Bodega).
/// </summary>
public class Unit
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CommunityId { get; set; }
    public Community Community { get; set; } = null!;

    /// <summary>
    /// Identificador visible de la unidad (ej. "101", "B-14").
    /// </summary>
    public string Number { get; set; } = null!;
    
    /// <summary>
    /// Porcentaje de participación o prorrateo original de la unidad.
    /// </summary>
    public decimal CoefficientPct { get; set; }
    
    public string? OwnerName { get; set; }
    public string? OwnerEmail { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<UserUnit> UserUnits { get; set; } = [];
    public ICollection<UnitComponent> Components { get; set; } = [];

    /// <summary>
    /// Obtiene el coeficiente total sumando los componentes activos si existen, de lo contrario devuelve el coeficiente principal.
    /// </summary>
    /// <returns>Valor decimal del coeficiente total.</returns>
    public decimal GetTotalCoefficientPct()
    {
        return Components.Count > 0 && Components.Any(x => x.IsActive)
            ? Components.Where(x => x.IsActive).Sum(x => x.CoefficientPct)
            : CoefficientPct;
    }
}
