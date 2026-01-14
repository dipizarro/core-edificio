using CoreEdificio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole<Guid>> roles,
        AppDbContext db)
    {
        // 1. Roles
        string[] roleNames = { AppRoles.Admin, AppRoles.Committee, AppRoles.Resident };

        foreach (var role in roleNames)
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole<Guid>(role));
        }

        // 2. Community + Unit base
        var community = await db.Communities.FirstOrDefaultAsync();
        if (community == null) return;

        var unit = await db.Units.FirstOrDefaultAsync();
        if (unit == null) return;

        await CreateUserIfNotExists(
            users,
            db,
            email: "admin@coreedificio.local",
            password: "Admin123!",
            role: AppRoles.Admin,
            community.Id,
            unit.Id
        );

        await CreateUserIfNotExists(
            users,
            db,
            email: "committee@coreedificio.local",
            password: "Committee123!",
            role: AppRoles.Committee,
            community.Id,
            unit.Id
        );

        await CreateUserIfNotExists(
            users,
            db,
            email: "resident@coreedificio.local",
            password: "Resident123!",
            role: AppRoles.Resident,
            community.Id,
            unit.Id
        );
    }

    private static async Task CreateUserIfNotExists(
        UserManager<ApplicationUser> users,
        AppDbContext db,
        string email,
        string password,
        string role,
        Guid communityId,
        Guid unitId)
    {
        var user = await users.FindByEmailAsync(email);
        if (user != null) return;

        user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            CommunityId = communityId, // Mantener para backfill/compatibilidad
            UnitId = unitId,           // Mantener para backfill/compatibilidad
            EmailConfirmed = true
        };

        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded) return;

        await users.AddToRoleAsync(user, role);

        // Crear asociación UserUnit
        var userUnit = new CoreEdificio.Domain.Entities.UserUnit
        {
            UserId = user.Id,
            UnitId = unitId,
            CommunityId = communityId,
            RelationshipType = role == AppRoles.Resident ? AppRoles.Resident : AppRoles.Admin,
            IsPrimary = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.UserUnits.Add(userUnit);
        await db.SaveChangesAsync();
    }
}
