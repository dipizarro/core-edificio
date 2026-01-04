using Microsoft.AspNetCore.Identity;

namespace CoreEdificio.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public Guid? CommunityId { get; set; }   // para scope por comunidad (MVP)
    public Guid? UnitId { get; set; }        // para Resident (MVP)
}
