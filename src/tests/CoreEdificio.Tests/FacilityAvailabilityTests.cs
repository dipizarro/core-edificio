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
    private static FacilitiesController CreateController(AppDbContext db)
    {
        var repo = new CoreEdificio.Infrastructure.Repositories.FacilityRepository(db);
        var communityRepo = new CoreEdificio.Infrastructure.Repositories.CommunityRepository(db);
        var service = new CoreEdificio.Application.Services.FacilityService(repo, communityRepo);
        return new FacilitiesController(service, db);
    }

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

        var controller = CreateController(db);
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
        var controller = CreateController(db);
        
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

        var controller = CreateController(db);
        SetUser(controller, communityId, "Resident");

        var result = await controller.GetAvailability(communityId, facilityId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<FacilityAvailabilityResponse>(okResult.Value);

        Assert.NotEmpty(response.Intervals);
        Assert.Null(response.Intervals[0].UnitNumber);
    }

    [Fact]
    public async Task GetAvailabilitySlots_GeneratesCorrectCount()
    {
        await using var db = await CreateDbAsync();
        var communityId = Guid.NewGuid();
        var facilityId = Guid.NewGuid();
        db.Facilities.Add(new Facility { Id = facilityId, CommunityId = communityId, Name = "Q", IsActive = true, SlotDurationMinutes = 60 });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        SetUser(controller, communityId, "Resident");

        var from = new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 1, 13, 0, 0, DateTimeKind.Utc); // 3 hours = 3 slots

        var result = await controller.GetAvailabilitySlots(communityId, facilityId, from, to, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<FacilityAvailabilitySlotsResponse>(okResult.Value);

        Assert.Equal(3, response.Slots.Count);
        Assert.Equal(from, response.Slots[0].StartAtUtc);
        Assert.Equal(to, response.Slots[2].EndAtUtc);
    }

    [Fact]
    public async Task GetAvailabilitySlots_BlockedWinsOverBooking()
    {
        await using var db = await CreateDbAsync();
        var communityId = Guid.NewGuid();
        var facilityId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        
        db.Communities.Add(new Community { Id = communityId, Name = "T", Address = "A" });
        db.Units.Add(new Unit { Id = unitId, CommunityId = communityId, Number = "101", CoefficientPct = 10m });
        db.Facilities.Add(new Facility { Id = facilityId, CommunityId = communityId, Name = "Q", IsActive = true, SlotDurationMinutes = 60 });

        var start = new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 2, 1, 11, 0, 0, DateTimeKind.Utc);

        // Booking Approved
        db.Bookings.Add(new Booking { CommunityId = communityId, FacilityId = facilityId, UnitId = unitId, StartAtUtc = start, EndAtUtc = end, Status = BookingStatus.Approved, CreatedByUserId = Guid.NewGuid() });

        // Block (Active) same time
        db.FacilityBlocks.Add(new FacilityBlock { CommunityId = communityId, FacilityId = facilityId, StartAtUtc = start, EndAtUtc = end, Reason = "Maintenance", IsActive = true, CreatedByUserId = Guid.NewGuid() });

        await db.SaveChangesAsync();

        var controller = CreateController(db);
        SetUser(controller, communityId, "Resident");

        var result = await controller.GetAvailabilitySlots(communityId, facilityId, start, end, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<FacilityAvailabilitySlotsResponse>(okResult.Value);

        Assert.Single(response.Slots);
        Assert.Equal("Blocked", response.Slots[0].Status);
        Assert.Equal("Maintenance", response.Slots[0].ReasonOrStatus);
    }

    [Fact]
    public async Task GetAvailabilitySlots_RangeLimit_ThrowsBadRequest()
    {
        await using var db = await CreateDbAsync();
        var controller = CreateController(db);
        var from = DateTime.UtcNow;
        var to = from.AddDays(15);
        var result = await controller.GetAvailabilitySlots(Guid.NewGuid(), Guid.NewGuid(), from, to, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
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
