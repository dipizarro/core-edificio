using CoreEdificio.Infrastructure.Identity;
using CoreEdificio.Infrastructure.Persistence;
using CoreEdificio.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CoreEdificio.Tests;

public class UserProvisioningServiceTests
{
    [Fact]
    public async Task CreateUserAsync_ResidentWithUnit_ShouldCreateUserUnitAssociation()
    {
        // Arrange
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            using var db = new AppDbContext(options);
            db.Database.EnsureCreated();

            var userStore = new Mock<IUserStore<ApplicationUser>>();
            var userManager = new Mock<UserManager<ApplicationUser>>(userStore.Object, null, null, null, null, null, null, null, null);
            
            var roleStore = new Mock<IRoleStore<IdentityRole<Guid>>>();
            var roleManager = new Mock<RoleManager<IdentityRole<Guid>>>(roleStore.Object, null, null, null, null);

            var residentRole = new IdentityRole<Guid>(CoreEdificio.Infrastructure.Identity.AppRoles.Resident) { NormalizedName = "RESIDENT" };
            var roles = new List<IdentityRole<Guid>> { residentRole }.AsQueryable();

            roleManager.Setup(x => x.Roles).Returns(roles);
            roleManager.Setup(x => x.NormalizeKey(It.IsAny<string>())).Returns((string s) => s.ToUpperInvariant());

            ApplicationUser? createdUser = null;
            userManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .Callback<ApplicationUser, string>((u, p) => {
                    createdUser = u;
                    db.Users.Add(u);
                    db.SaveChanges();
                })
                .ReturnsAsync(IdentityResult.Success);
            
            userManager.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            var service = new UserProvisioningService(userManager.Object, roleManager.Object, db);
            
            var communityId = Guid.NewGuid();
            var unitId = Guid.NewGuid();

            var community = new Community { Id = communityId, Name = "Test Community", Address = "123 St" };
            var unit = new Unit { Id = unitId, CommunityId = communityId, Number = "101" };
            db.Communities.Add(community);
            db.Units.Add(unit);
            await db.SaveChangesAsync();

            var email = "test@resident.com";

            // Act
            var user = await service.CreateUserAsync(email, "Pass123!", "reSidenT", communityId, unitId);

            // Assert
            var userUnit = await db.UserUnits.FirstOrDefaultAsync(uu => 
                uu.UserId == user.Id && 
                uu.UnitId == unitId);
            
            Assert.NotNull(userUnit);
            Assert.Equal(CoreEdificio.Infrastructure.Identity.AppRoles.Resident, userUnit.RelationshipType);
            Assert.True(userUnit.IsPrimary);
        }
        finally
        {
            connection.Close();
        }
    }
}
