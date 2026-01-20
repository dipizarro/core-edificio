using CoreEdificio.Application.Common;
using CoreEdificio.Application.Services;
using CoreEdificio.Domain.Entities;
using CoreEdificio.Infrastructure.Persistence;
using CoreEdificio.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Tests;

public class BookingServiceTests
{
    [Fact]
    public async Task CreateBooking_WithoutApproval_SetsApproved()
    {
        await using var db = await CreateDbAsync();
        var community = await SeedCommunityAsync(db);
        var unit = await SeedUnitAsync(db, community.Id);
        var facility = await SeedFacilityAsync(db, community.Id, requiresApproval: false, slotMinutes: 60);

        var service = CreateService(db);
        var start = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc);

        var booking = await service.CreateBookingAsync(community.Id, facility.Id, unit.Id, Guid.NewGuid(), start, end, null);

        Assert.Equal(BookingStatus.Approved, booking.Status);
    }

    [Fact]
    public async Task CreateBooking_WithApproval_SetsPendingApproval()
    {
        await using var db = await CreateDbAsync();
        var community = await SeedCommunityAsync(db);
        var unit = await SeedUnitAsync(db, community.Id);
        var facility = await SeedFacilityAsync(db, community.Id, requiresApproval: true, slotMinutes: 60);

        var service = CreateService(db);
        var start = new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 2, 11, 0, 0, DateTimeKind.Utc);

        var booking = await service.CreateBookingAsync(community.Id, facility.Id, unit.Id, Guid.NewGuid(), start, end, null);

        Assert.Equal(BookingStatus.PendingApproval, booking.Status);
    }

    [Fact]
    public async Task CreateBooking_WithOverlap_ThrowsConflict()
    {
        await using var db = await CreateDbAsync();
        var community = await SeedCommunityAsync(db);
        var unit = await SeedUnitAsync(db, community.Id);
        var facility = await SeedFacilityAsync(db, community.Id, requiresApproval: false, slotMinutes: 60);

        db.Bookings.Add(new Booking
        {
            CommunityId = community.Id,
            FacilityId = facility.Id,
            UnitId = unit.Id,
            CreatedByUserId = Guid.NewGuid(),
            StartAtUtc = new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc),
            EndAtUtc = new DateTime(2026, 1, 3, 11, 0, 0, DateTimeKind.Utc),
            Status = BookingStatus.Approved,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var start = new DateTime(2026, 1, 3, 10, 30, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 3, 11, 30, 0, DateTimeKind.Utc);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateBookingAsync(community.Id, facility.Id, unit.Id, Guid.NewGuid(), start, end, null));
    }

    [Fact]
    public async Task CreateBooking_WithInvalidDuration_ThrowsValidation()
    {
        await using var db = await CreateDbAsync();
        var community = await SeedCommunityAsync(db);
        var unit = await SeedUnitAsync(db, community.Id);
        var facility = await SeedFacilityAsync(db, community.Id, requiresApproval: false, slotMinutes: 60);

        var service = CreateService(db);
        var start = new DateTime(2026, 1, 4, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 4, 10, 30, 0, DateTimeKind.Utc);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateBookingAsync(community.Id, facility.Id, unit.Id, Guid.NewGuid(), start, end, null));
    }

    private static BookingService CreateService(AppDbContext db)
    {
        var repo = new BookingRepository(db);
        return new BookingService(repo);
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

    private static async Task<Community> SeedCommunityAsync(AppDbContext db)
    {
        var community = new Community { Name = "Test", Address = "Address" };
        db.Communities.Add(community);
        await db.SaveChangesAsync();
        return community;
    }

    private static async Task<Unit> SeedUnitAsync(AppDbContext db, Guid communityId)
    {
        var unit = new Unit { CommunityId = communityId, Number = "101", CoefficientPct = 10m };
        db.Units.Add(unit);
        await db.SaveChangesAsync();
        return unit;
    }

    private static async Task<Facility> SeedFacilityAsync(AppDbContext db, Guid communityId, bool requiresApproval, int slotMinutes)
    {
        var facility = new Facility
        {
            CommunityId = communityId,
            Name = "Quincho",
            ChargingMode = FacilityChargingMode.Free,
            RentAmountClp = 0,
            DepositAmountClp = 0,
            RequiresApproval = requiresApproval,
            SlotDurationMinutes = slotMinutes,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Facilities.Add(facility);
        await db.SaveChangesAsync();
        return facility;
    }
}
