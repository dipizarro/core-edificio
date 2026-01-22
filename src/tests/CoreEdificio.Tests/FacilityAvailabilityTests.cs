using System.Security.Claims;
using CoreEdificio.Api.Auth;
using CoreEdificio.Api.Contracts;
using CoreEdificio.Api.Controllers;
using CoreEdificio.Domain.Entities;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoreEdificio.Tests;

public class FacilityAvailabilityTests
{
    [Fact]
    public async Task GetAvailability_ReturnsMergedResults()
    {
        await using var db = await CreateDbAsync();
        var communityId = Guid.NewGuid();
        var facilityId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        
        db.Communities.Add(new Community { Id = communityId, Name = "Test", Address = "..." });
        db.Units.Add(new Unit { Id = unitId, CommunityId = communityId, Number = "101", CoefficientPct = 10m });
        db.Facilities.Add(new Facility 
        { 
            Id = facilityId, 
            CommunityId = communityId, 
            Name = "Quincho", 
            IsActive = true, 
            SlotDurationMinutes = 60,
            ChargingMode = FacilityChargingMode.Free
        });

        // Booking: Approved
        db.Bookings.Add(new Booking
        {
            CommunityId = communityId,
            FacilityId = facilityId,
            UnitId = unitId,
            StartAtUtc = new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc),
            EndAtUtc = new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc),
            Status = BookingStatus.Approved,
            CreatedByUserId = Guid.NewGuid()
        });

        // Block: Active
        db.FacilityBlocks.Add(new FacilityBlock
        {
            CommunityId = communityId,
            FacilityId = facilityId,
            StartAtUtc = new DateTime(2026, 2, 1, 14, 0, 0, DateTimeKind.Utc),
            EndAtUtc = new DateTime(2026, 2, 1, 16, 0, 0, DateTimeKind.Utc),
            Reason = "Cleaning",
            IsActive = true,
            CreatedByUserId = Guid.NewGuid()
        });

        await db.SaveChangesAsync();

        var controller = new FacilitiesController(db);
        SetUser(controller, communityId, "Committee");

        var from = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc);

        var result = await controller.GetAvailability(communityId, facilityId, from, to, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<FacilityAvailabilityResponse>(okResult.Value);

        Assert.Equal(2, response.Intervals.Count);
        Assert.Equal("Booking", response.Intervals[0].Kind);
        Assert.Equal("101", response.Intervals[0].UnitNumber);
        Assert.Equal("Block", response.Intervals[1].Kind);
        Assert.Null(response.Intervals[1].UnitNumber);
    }

    [Fact]
    public async Task GetAvailability_RespectsRangeLimit()
    {
        await using var db = await CreateDbAsync();
        var controller = new FacilitiesController(db);
        
        var from = DateTime.UtcNow;
        var to = from.AddDays(32);

        var result = await controller.GetAvailability(Guid.NewGuid(), Guid.NewGuid(), from, to, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetAvailability_RoleBasedVisibility_ResidentSeesNoUnitNumber()
    {
        await using var db = await CreateDbAsync();
        var communityId = Guid.NewGuid();
        var facilityId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        
        db.Communities.Add(new Community { Id = communityId, Name = "Test", Address = "..." });
        db.Units.Add(new Unit { Id = unitId, CommunityId = communityId, Number = "101", CoefficientPct = 10m });
        db.Facilities.Add(new Facility { Id = facilityId, CommunityId = communityId, Name = "Q", IsActive = true, SlotDurationMinutes = 60 });
        db.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            CommunityId = communityId,
            FacilityId = facilityId,
            UnitId = unitId,
            StartAtUtc = DateTime.UtcNow.AddHours(1),
            EndAtUtc = DateTime.UtcNow.AddHours(2),
            Status = BookingStatus.Approved,
            CreatedByUserId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var controller = new FacilitiesController(db);
        SetUser(controller, communityId, "Resident");

        var result = await controller.GetAvailability(communityId, facilityId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<FacilityAvailabilityResponse>(okResult.Value);

        Assert.NotEmpty(response.Intervals);
        Assert.Null(response.Intervals[0].UnitNumber);
    }

    private static void SetUser(ControllerBase controller, Guid communityId, string role)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, role),
            new Claim(AuthClaims.CommunityId, communityId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    private static async Task<AppDbContext> CreateDbAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }
}
