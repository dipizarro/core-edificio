using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CoreEdificio.Infrastructure.Persistence;
using CoreEdificio.Domain.Entities;

namespace CoreEdificio.Infrastructure.Identity;

public class UserProvisioningService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole<Guid>> _roles;
    private readonly AppDbContext _db;

    public UserProvisioningService(
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole<Guid>> roles,
        AppDbContext db)
    {
        _users = users;
        _roles = roles;
        _db = db;
    }

    /// <summary>
    /// Crea un usuario y le asigna un rol. El parámetro role es case-insensitive.
    /// </summary>
    public async Task<ApplicationUser> CreateUserAsync(
        string email,
        string password,
        string role,
        Guid? communityId,
        Guid? unitId)
    {
        var normalizedRole = _roles.NormalizeKey(role.Trim());
        var roleEntity = await _roles.Roles.SingleOrDefaultAsync(r => r.NormalizedName == normalizedRole);
        
        if (roleEntity is null)
            throw new InvalidOperationException($"Role '{role}' does not exist");

        var roleName = roleEntity.Name!;

        if (string.Equals(roleName, AppRoles.Resident, StringComparison.OrdinalIgnoreCase) && unitId is null)
            throw new InvalidOperationException("Resident must have UnitId");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            CommunityId = communityId,
            UnitId = unitId,
            EmailConfirmed = true
        };

        var result = await _users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" | ", result.Errors.Select(e => e.Description)));

        await _users.AddToRoleAsync(user, roleName);

        // Si es Residente y tiene UnitId, crear relación UserUnit
        if (string.Equals(roleName, AppRoles.Resident, StringComparison.OrdinalIgnoreCase) && unitId.HasValue && communityId.HasValue)
        {
            var userUnit = new UserUnit
            {
                UserId = user.Id,
                UnitId = unitId.Value,
                CommunityId = communityId.Value,
                RelationshipType = AppRoles.Resident,
                IsPrimary = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.UserUnits.Add(userUnit);
            await _db.SaveChangesAsync();
        }

        return user;
    }
}
