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
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(userStore.Object, null, null, null, null, null, null, null, null);
        
        var roleStore = new Mock<IRoleStore<IdentityRole<Guid>>>();
        var roleManager = new Mock<RoleManager<IdentityRole<Guid>>>(roleStore.Object, null, null, null, null);

        // Mock AppDbContext y DbSet
        var options = new DbContextOptionsBuilder<AppDbContext>().Options;
        var dbContextMock = new Mock<AppDbContext>(options);
        var userUnitsMock = new Mock<DbSet<UserUnit>>();
        
        dbContextMock.Setup(x => x.UserUnits).Returns(userUnitsMock.Object);

        userManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .Callback<ApplicationUser, string>((u, p) => u.Id = Guid.NewGuid()) 
            .ReturnsAsync(IdentityResult.Success);
        
        userManager.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        roleManager.Setup(x => x.RoleExistsAsync("Resident"))
            .ReturnsAsync(true);

        var service = new UserProvisioningService(userManager.Object, roleManager.Object, dbContextMock.Object);
        
        var email = "test@resident.com";
        var communityId = Guid.NewGuid();
        var unitId = Guid.NewGuid();

        // Act
        var user = await service.CreateUserAsync(email, "Pass123!", "Resident", communityId, unitId);

        // Assert
        userUnitsMock.Verify(x => x.Add(It.Is<UserUnit>(uu => 
            uu.UserId == user.Id && 
            uu.UnitId == unitId && 
            uu.RelationshipType == "Resident")), Times.Once);
        
        dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
