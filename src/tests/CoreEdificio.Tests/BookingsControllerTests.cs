using System.Security.Claims;
using CoreEdificio.Api.Auth;
using CoreEdificio.Api.Contracts;
using CoreEdificio.Api.Controllers;
using CoreEdificio.Application.Services;
using CoreEdificio.Domain.Entities;
using CoreEdificio.Domain.Entities.Billing;
using CoreEdificio.Infrastructure.Persistence;
using CoreEdificio.Infrastructure.Repositories;
using CoreEdificio.Infrastructure.Repositories.Billing;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Tests;

public class BookingsControllerTests
{
    [Fact]
    public async Task ResidentBookingForOtherUnit_ReturnsForbid()
    {
        await using var db = await CreateDbAsync();
        var service = new BookingService(new BookingRepository(db), new ChargeRepository(db), new FacilityBlockRepository(db), new UnitOfWork(db));
        var controller = new BookingsController(service, db);

        var residentUnitId = Guid.NewGuid();
        var requestUnitId = Guid.NewGuid();

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(AuthClaims.UnitId, residentUnitId.ToString()),
            new Claim(ClaimTypes.Role, "Resident")
        }, "TestAuth"));

        var httpContext = new DefaultHttpContext { User = user };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new CreateBookingRequest(
            requestUnitId,
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(1),
            null);

        var result = await controller.Create(Guid.NewGuid(), Guid.NewGuid(), request, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
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

    [Fact]
    public async Task GetById_ResidentOwnBooking_ReturnsOk()
    {
        await using var db = await CreateDbAsync();
        var service = new BookingService(new BookingRepository(db), new ChargeRepository(db), new FacilityBlockRepository(db), new UnitOfWork(db));
        var controller = new BookingsController(service, db);

        var commId = Guid.NewGuid();
        var facId = Guid.NewGuid();
        var uId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        db.Communities.Add(new Community { Id = commId, Name = "Test Community", Address = "Test Address", CreatedAtUtc = DateTime.UtcNow }); // Added Community
        db.Bookings.Add(new Booking { Id = bookingId, CommunityId = commId, FacilityId = facId, UnitId = uId, Status = BookingStatus.Approved, StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddHours(1), CreatedByUserId = Guid.NewGuid() });
        db.Facilities.Add(new Facility { Id = facId, Name = "Gym", CommunityId = commId, IsActive = true, ChargingMode = FacilityChargingMode.Free, RentAmountClp = 0, DepositAmountClp = 0, RequiresApproval = false, SlotDurationMinutes = 60, CreatedAtUtc = DateTime.UtcNow });
        db.Units.Add(new Unit { Id = uId, Number = "101", CommunityId = commId, CoefficientPct = 1.0m, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(AuthClaims.UnitId, uId.ToString()),
            new Claim(ClaimTypes.Role, "Resident")
        }, "TestAuth"));
        
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };

        var result = await controller.GetById(bookingId, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task Complete_Approved_SetsStatus()
    {
        await using var db = await CreateDbAsync();
        var service = new BookingService(new BookingRepository(db), new ChargeRepository(db), new FacilityBlockRepository(db), new UnitOfWork(db));
        var controller = new BookingsController(service, db);

        var commId = Guid.NewGuid();
        var facId = Guid.NewGuid();
        var uId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        db.Communities.Add(new Community { Id = commId, Name = "Test Community", Address = "Test Address", CreatedAtUtc = DateTime.UtcNow }); // Added Community
        db.Bookings.Add(new Booking { Id = bookingId, CommunityId = commId, FacilityId = facId, UnitId = uId, Status = BookingStatus.Approved, StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddHours(1), CreatedByUserId = Guid.NewGuid() });
        db.Facilities.Add(new Facility { Id = facId, Name = "Gym", CommunityId = commId, IsActive = true, ChargingMode = FacilityChargingMode.Free, RentAmountClp = 0, DepositAmountClp = 0, RequiresApproval = false, SlotDurationMinutes = 60, CreatedAtUtc = DateTime.UtcNow });
        db.Units.Add(new Unit { Id = uId, Number = "101", CommunityId = commId, CoefficientPct = 1.0m, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("community_id", commId.ToString()),
            new Claim(ClaimTypes.Role, "Admin")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };

        var result = await controller.Complete(bookingId, CancellationToken.None);
        Assert.IsType<NoContentResult>(result);

        var updated = await db.Bookings.FindAsync(bookingId);
        Assert.Equal(BookingStatus.Completed, updated?.Status);
        Assert.NotNull(updated?.CompletedAtUtc);
    }
}
