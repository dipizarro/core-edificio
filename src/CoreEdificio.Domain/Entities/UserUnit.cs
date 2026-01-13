
namespace CoreEdificio.Domain.Entities;

public class UserUnit
{
    public Guid UserId { get; set; }
    // Navigation property back to ApplicationUser is in Infrastructure, so we can't define it here type-safely 
    // unless we introduce an interface or similar pattern. 
    // However, EF Core allows defining navigation without the type if configured strictly, 
    // OR normally we put logic in Infrastructure.
    // BUT, the requirement is to have navigation in Unit -> UserUnit.
    // So UserUnit MUST be in Domain.
    // ApplicationUser is in Infrastructure.
    // Domain cannot reference Infrastructure.
    // So UserUnit cannot have "public ApplicationUser User { get; set; }"
    // WE WILL OMIT the User navigation property here for now, or use Object/dynamic (bad).
    // Better strategy: Only Unit->UserUnit navigation is strongly typed. 
    // User->UserUnit is strongly typed in ApplicationUser (Infra).
    // Relation config in DbContext will handle binding.
    
    public Guid UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public Guid CommunityId { get; set; }

    public string RelationshipType { get; set; } = "Resident"; // Owner, Tenant, Resident
    public bool IsPrimary { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
