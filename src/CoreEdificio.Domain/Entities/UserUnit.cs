namespace CoreEdificio.Domain.Entities;

/// <summary>
/// Entidad de unión que representa la relación entre un Usuario (definido en infraestructura) y una Unidad.
/// </summary>
public class UserUnit
{
    /// <summary>
    /// Identificador del usuario (ApplicationUser).
    /// </summary>
    public Guid UserId { get; set; }
    
    /* 
     * Nota Arquitectónica: 
     * La propiedad de navegación hacia ApplicationUser se omite aquí para no romper Clean Architecture, 
     * ya que ApplicationUser reside en la capa Infrastructure y Domain no debe conocerla.
     * La relación se mapea vía EF Core fluent API en el DbContext.
     */
    
    public Guid UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public Guid CommunityId { get; set; }

    /// <summary>
    /// Tipo de relación con la unidad (Owner, Tenant, Resident).
    /// </summary>
    public string RelationshipType { get; set; } = "Resident";
    
    /// <summary>
    /// Indica si es la unidad principal para el usuario en la comunidad.
    /// </summary>
    public bool IsPrimary { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
