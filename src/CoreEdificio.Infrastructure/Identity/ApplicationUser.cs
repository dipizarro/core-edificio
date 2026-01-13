using Microsoft.AspNetCore.Identity;

namespace CoreEdificio.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    [Obsolete("Use UserUnits for many-to-many. This is kept for migration support.")]
    public Guid? CommunityId { get; set; }   // para scope por comunidad (MVP)
    
    [Obsolete("Use UserUnits for many-to-many. This is kept for migration support.")]
    public Guid? UnitId { get; set; }        // para Resident (MVP)

    public ICollection<CoreEdificio.Domain.Entities.UserUnit> UserUnits { get; set; } = new List<CoreEdificio.Domain.Entities.UserUnit>();
}
