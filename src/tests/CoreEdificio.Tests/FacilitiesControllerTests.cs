using System.Security.Claims;
using CoreEdificio.Application.Common;
using CoreEdificio.Api.Auth;
using CoreEdificio.Api.Contracts;
using CoreEdificio.Api.Controllers;
using CoreEdificio.Domain.Entities;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Tests;

public class FacilitiesControllerTests
{
    private static FacilitiesController CreateController(AppDbContext db)
    {
        var repo = new CoreEdificio.Infrastructure.Repositories.FacilityRepository(db);
        var communityRepo = new CoreEdificio.Infrastructure.Repositories.CommunityRepository(db);
        var service = new CoreEdificio.Application.Services.FacilityService(repo, communityRepo);
        return new FacilitiesController(service, db);
    }

    [Fact]
    public async Task CreateFreeWithAmounts_ReturnsBadRequest()
    {
        await using var db = await CreateDbAsync();
        var community = new Community { Name = "Test", Address = "Address" };
        db.Communities.Add(community);
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var request = new CreateFacilityRequest(
            "Sala",
            null,
            null,
            "Free",
            1000,
            0,
            false,
            60,
            null,
            null);

        await Assert.ThrowsAsync<ValidationException>(() => controller.Create(community.Id, request, CancellationToken.None));
    }

    [Fact]
    public async Task CreatePaidWithZeroRent_ReturnsBadRequest()
    {
        await using var db = await CreateDbAsync();
        var community = new Community { Name = "Test", Address = "Address" };
        db.Communities.Add(community);
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var request = new CreateFacilityRequest(
            "Sala",
            null,
            null,
            "Paid",
            0,
            0,
            false,
            60,
            null,
            null);

        await Assert.ThrowsAsync<ValidationException>(() => controller.Create(community.Id, request, CancellationToken.None));
    }

    [Fact]
    public async Task CreatePaidAndDepositWithAmounts_ReturnsCreated()
    {
        await using var db = await CreateDbAsync();
        var community = new Community { Name = "Test", Address = "Address" };
        db.Communities.Add(community);
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var request = new CreateFacilityRequest(
            "Quincho",
            null,
            null,
            "PaidAndDeposit",
            30000,
            50000,
            true,
            60,
            null,
            null);

        var result = await controller.Create(community.Id, request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var dto = Assert.IsType<FacilityDto>(created.Value);
        Assert.Equal(community.Id, dto.CommunityId);
    }

    [Fact]
    public async Task CommunityScopeHandler_MismatchedCommunity_DoesNotSucceed()
    {
        var handler = new CommunityScopeHandler();
        var requirement = new CommunityScopeRequirement();

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AuthClaims.CommunityId, Guid.NewGuid().ToString())
        }, "Test"));

        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["communityId"] = Guid.NewGuid().ToString();

        var authContext = new AuthorizationHandlerContext(new[] { requirement }, user, httpContext);

        await handler.HandleAsync(authContext);

        Assert.False(authContext.HasSucceeded);
    }

    private static async Task<AppDbContext> CreateDbAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }
}
