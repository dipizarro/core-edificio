using Microsoft.AspNetCore.Identity;

namespace CoreEdificio.Infrastructure.Identity;

public class UserProvisioningService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole<Guid>> _roles;

    public UserProvisioningService(
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole<Guid>> roles)
    {
        _users = users;
        _roles = roles;
    }

    public async Task<ApplicationUser> CreateUserAsync(
        string email,
        string password,
        string role,
        Guid? communityId,
        Guid? unitId)
    {
        if (!await _roles.RoleExistsAsync(role))
            throw new InvalidOperationException($"Role '{role}' does not exist");

        if (role == "Resident" && unitId is null)
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

        await _users.AddToRoleAsync(user, role);

        return user;
    }
}
