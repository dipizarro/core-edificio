using CoreEdificio.Application.Contracts.Residents;
using CoreEdificio.Application.Interfaces.Identity;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Identity;

public class IdentityService : IIdentityService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public IdentityService(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<List<ResidentWithUnitsDto>> GetResidentsWithUnitsAsync(Guid communityId, CancellationToken ct = default)
    {
        // Traer usuarios que tengan al menos una relación en la comunidad
        // O filtrar por ApplicationUser.CommunityId si queremos ser conservadores (backfill)
        // El requerimiento dice: "Trae usuarios de esa comunidad que sean Resident (y opcionalmente Owner/Tenant si existen)"
        
        var query = _db.Users
            .Include(u => u.UserUnits)
                .ThenInclude(uu => uu.Unit)
            .Where(u => u.UserUnits.Any(uu => uu.CommunityId == communityId))
            .AsQueryable();

        var users = await query.ToListAsync(ct);

        var result = new List<ResidentWithUnitsDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var mainRole = roles.FirstOrDefault() ?? "Unknown";

            var units = user.UserUnits
                .Where(uu => uu.CommunityId == communityId)
                .Select(uu => new UnitRefDto(uu.UnitId, uu.Unit.Number))
                .ToList();

            if (units.Any())
            {
                result.Add(new ResidentWithUnitsDto(
                    user.Id,
                    user.Email ?? "",
                    mainRole,
                    units
                ));
            }
        }

        return result.OrderBy(r => r.Email).ToList();
    }
}
