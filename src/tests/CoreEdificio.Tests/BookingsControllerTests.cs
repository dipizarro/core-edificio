using System.Security.Claims;
using CoreEdificio.Api.Auth;
using CoreEdificio.Api.Contracts;
using CoreEdificio.Api.Controllers;
using CoreEdificio.Application.Services;
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
}
