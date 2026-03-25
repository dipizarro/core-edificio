using CoreEdificio.Domain.Entities;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoreEdificio.Infrastructure.Identity;

/// <summary>
/// Clase responsable de poblar la base de datos con roles y usuarios por defecto.
/// Utilizado principalmente en entornos de desarrollo.
/// </summary>
public static class IdentitySeeder
{
    /// <summary>
    /// Ejecuta el proceso de siembra (seeding) de la identidad del sistema.
    /// Crea roles predeterminados y usuarios administrativos si es que el ambiente tiene una comunidad creada.
    /// </summary>
    public static async Task SeedAsync(
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole<Guid>> roles,
        AppDbContext db,
        ILogger? logger = null)
    {
        // 1. Crear Roles del sistema
        string[] roleNames = [ AppRoles.Admin, AppRoles.Committee, AppRoles.Resident ];

        foreach (var role in roleNames)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        // 2. Obtener comunidad y unidad base para la asociación de usuarios
        var community = await db.Communities.FirstOrDefaultAsync();
        if (community == null)
        {
            logger?.LogWarning("IdentitySeeder: No se encontró una Comunidad base. Se aborta la creación de usuarios predeterminados.");
            return;
        }

        // 3. Generar Instalaciones (Facilities) de prueba
        if (!await db.Facilities.AnyAsync(f => f.CommunityId == community.Id))
        {
            db.Facilities.AddRange(
                new Facility
                {
                    CommunityId = community.Id,
                    Name = "Quincho",
                    Description = "Espacio para asados y reuniones.",
                    IsActive = true,
                    ChargingMode = FacilityChargingMode.PaidAndDeposit,
                    RentAmountClp = 30000,
                    DepositAmountClp = 50000,
                    RequiresApproval = true,
                    SlotDurationMinutes = 60,
                    CreatedAtUtc = DateTime.UtcNow
                },
                new Facility
                {
                    CommunityId = community.Id,
                    Name = "Sala Reuniones",
                    Description = "Espacio multiuso para reuniones.",
                    IsActive = true,
                    ChargingMode = FacilityChargingMode.Free,
                    RentAmountClp = 0,
                    DepositAmountClp = 0,
                    RequiresApproval = false,
                    SlotDurationMinutes = 60,
                    CreatedAtUtc = DateTime.UtcNow
                });

            await db.SaveChangesAsync();
        }

        var unit = await db.Units.FirstOrDefaultAsync();
        if (unit == null)
        {
            logger?.LogWarning("IdentitySeeder: No se encontró una Unidad base. Se aborta la creación de usuarios predeterminados.");
            return;
        }

        // TODO: En ambientes productivos, estas contraseñas deben extraerse del IConfiguration de secretos
        await CreateUserIfNotExistsAsync(users, db, "admin@coreedificio.local", "Admin123!", AppRoles.Admin, community.Id, unit.Id);
        await CreateUserIfNotExistsAsync(users, db, "committee@coreedificio.local", "Committee123!", AppRoles.Committee, community.Id, unit.Id);
        await CreateUserIfNotExistsAsync(users, db, "resident@coreedificio.local", "Resident123!", AppRoles.Resident, community.Id, unit.Id);
    }

    /// <summary>
    /// Crea un usuario si no existe, lo asigna a un rol y crea su relación (UserUnit) en la comunidad.
    /// </summary>
    private static async Task CreateUserIfNotExistsAsync(
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
            CommunityId = communityId, // Mantenido para rest-compatibility
            UnitId = unitId,           // Mantenido para rest-compatibility
            EmailConfirmed = true
        };

        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded) return;

        await users.AddToRoleAsync(user, role);

        var userUnit = new UserUnit
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
