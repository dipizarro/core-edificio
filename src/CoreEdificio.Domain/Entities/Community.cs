namespace CoreEdificio.Domain.Entities;

/// <summary>
/// Representa una comunidad o edificio administrado por el sistema.
/// </summary>
public class Community
{
    /// <summary>
    /// Identificador único de la comunidad.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Nombre de la comunidad (ej. Edificio Los Reyes).
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Dirección física de la comunidad.
    /// </summary>
    public string Address { get; set; } = null!;

    /// <summary>
    /// Fecha de registro en formato UTC.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Unidades (departamentos, bodegas, etc.) asociadas a la comunidad.
    /// </summary>
    public List<Unit> Units { get; set; } = [];
}
