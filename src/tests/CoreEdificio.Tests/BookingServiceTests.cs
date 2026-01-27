using CoreEdificio.Application.Common;
using CoreEdificio.Application.Services;
using CoreEdificio.Domain.Entities;
using CoreEdificio.Infrastructure.Persistence;
using CoreEdificio.Infrastructure.Repositories;
using CoreEdificio.Infrastructure.Repositories.Billing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Tests;

public class BookingServiceTests
{
    [Fact]
    public async Task CreateBooking_WithoutApproval_SetsApproved_AndGeneratesCharges()
    {
        await using var db = await CreateDbAsync();
        var community = await SeedCommunityAsync(db);
        var unit = await SeedUnitAsync(db, community.Id);
        var facility = await SeedFacilityAsync(db, community.Id, requiresApproval: false, slotMinutes: 60, FacilityChargingMode.Paid, rent: 10000);

        var service = CreateService(db);
        var start = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc);

        var booking = await service.CreateBookingAsync(community.Id, facility.Id, unit.Id, Guid.NewGuid(), start, end, null);

        Assert.Equal(BookingStatus.Approved, booking.Status);
        
        var charges = await db.Charges.Where(c => c.SourceId == booking.Id).ToListAsync();
        Assert.Single(charges);
        Assert.Equal(10000, charges[0].Amount);
        Assert.Equal("Rent", charges[0].ChargeKind);
    }

    [Fact]
    public async Task CreateBooking_WithApproval_SetsPendingApproval_NoCharges()
    {
        await using var db = await CreateDbAsync();
        var community = await SeedCommunityAsync(db);
        var unit = await SeedUnitAsync(db, community.Id);
        var facility = await SeedFacilityAsync(db, community.Id, requiresApproval: true, slotMinutes: 60, FacilityChargingMode.Paid, rent: 10000);

        var service = CreateService(db);
        var start = new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 2, 11, 0, 0, DateTimeKind.Utc);

        var booking = await service.CreateBookingAsync(community.Id, facility.Id, unit.Id, Guid.NewGuid(), start, end, null);

        Assert.Equal(BookingStatus.PendingApproval, booking.Status);
        
        var chargesCount = await db.Charges.CountAsync(c => c.SourceId == booking.Id);
        Assert.Equal(0, chargesCount);
    }

    [Fact]
    public async Task ApproveBooking_GeneratesCharges_Once()
    {
        await using var db = await CreateDbAsync();
        var community = await SeedCommunityAsync(db);
        var unit = await SeedUnitAsync(db, community.Id);
        var facility = await SeedFacilityAsync(db, community.Id, requiresApproval: true, slotMinutes: 60, FacilityChargingMode.Paid, rent: 10000);

        var service = CreateService(db);
        var start = new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 3, 11, 0, 0, DateTimeKind.Utc);

        var booking = await service.CreateBookingAsync(community.Id, facility.Id, unit.Id, Guid.NewGuid(), start, end, null);
        
        // Act: Approve 1st time
        await service.ApproveBookingAsync(community.Id, facility.Id, booking.Id, Guid.NewGuid());
        
        var charges = await db.Charges.Where(c => c.SourceId == booking.Id).ToListAsync();
        Assert.Single(charges);

        // Act: Manual call or simulate double approve (validation would throw, but service method internal GenerateCharges is what we check for idempotency)
        // If we call GenerateCharges again manually (mocking internal logic)
        await service.GenerateChargesForBookingAsync(booking, facility, CancellationToken.None);
        
        charges = await db.Charges.Where(c => c.SourceId == booking.Id).ToListAsync();
        Assert.Single(charges); // Still 1
    }

    [Fact]
    public async Task CancelBooking_SetsStatus()
    {
        await using var db = await CreateDbAsync();
        var community = await SeedCommunityAsync(db);
        var unit = await SeedUnitAsync(db, community.Id);
        var facility = await SeedFacilityAsync(db, community.Id, requiresApproval: false, slotMinutes: 60);

        var service = CreateService(db);
        var start = new DateTime(2026, 1, 4, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 4, 11, 0, 0, DateTimeKind.Utc);
        var booking = await service.CreateBookingAsync(community.Id, facility.Id, unit.Id, Guid.NewGuid(), start, end, null);

        await service.CancelBookingAsync(booking.Id, Guid.NewGuid(), "User changed mind");

        var updated = await db.Bookings.FindAsync(booking.Id);
        Assert.Equal(BookingStatus.Cancelled, updated?.Status);
        Assert.Equal("User changed mind", updated?.CancelReason);
    }

    [Fact]
    public async Task CreateBooking_WithFacilityBlock_ThrowsValidation()
    {
        await using var db = await CreateDbAsync();
        var community = await SeedCommunityAsync(db);
        var unit = await SeedUnitAsync(db, community.Id);
        var facility = await SeedFacilityAsync(db, community.Id, requiresApproval: false, slotMinutes: 60);

        // Seed an active block
        var start = new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc);
        db.FacilityBlocks.Add(new FacilityBlock
        {
            CommunityId = community.Id,
            FacilityId = facility.Id,
            StartAtUtc = start,
            EndAtUtc = end,
            Reason = "Maintenance",
            IsActive = true,
            CreatedByUserId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        
        // Act & Assert: Choque total
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateBookingAsync(community.Id, facility.Id, unit.Id, Guid.NewGuid(), start, start.AddMinutes(60), null));

        // Act & Assert: Choque parcial (block empieza a la mitad del booking)
        var preStart = start.AddMinutes(-30);
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateBookingAsync(community.Id, facility.Id, unit.Id, Guid.NewGuid(), preStart, preStart.AddMinutes(60), null));
    }

    [Fact]
    public async Task CreateBooking_AfterBlockDeactivated_SetsApproved()
    {
        await using var db = await CreateDbAsync();
        var community = await SeedCommunityAsync(db);
        var unit = await SeedUnitAsync(db, community.Id);
        var facility = await SeedFacilityAsync(db, community.Id, requiresApproval: false, slotMinutes: 60);

        var start = new DateTime(2026, 2, 2, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 2, 2, 11, 0, 0, DateTimeKind.Utc);
        
        var block = new FacilityBlock
        {
            CommunityId = community.Id,
            FacilityId = facility.Id,
            StartAtUtc = start,
            EndAtUtc = end,
            Reason = "Temporary",
            IsActive = false, // INACTIVO
            CreatedByUserId = Guid.NewGuid()
        };
        db.FacilityBlocks.Add(block);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        
        var booking = await service.CreateBookingAsync(community.Id, facility.Id, unit.Id, Guid.NewGuid(), start, end, null);

        Assert.Equal(BookingStatus.Approved, booking.Status);
    }

    [Fact]
    public async Task CancelBooking_WithinPenaltyWindow_GeneratesFine()
    {
        await using var db = await CreateDbAsync();
        var community = await SeedCommunityAsync(db);
        var unit = await SeedUnitAsync(db, community.Id);
        // Window: 24h, Fine: 5000
        var facility = await SeedFacilityAsync(db, community.Id, false, 60, penaltyHours: 24, lateCancelFine: 5000);

        var service = CreateService(db);
        var start = DateTime.UtcNow.AddHours(2); // In 2 hours (within 24h)
        var end = start.AddHours(1);
        var booking = await service.CreateBookingAsync(community.Id, facility.Id, unit.Id, Guid.NewGuid(), start, end, null);

        // Act
        await service.CancelBookingAsync(booking.Id, Guid.NewGuid(), "Late cancel");

        // Assert
        var fine = await db.Charges.FirstOrDefaultAsync(c => c.SourceId == booking.Id && c.ChargeKind == "Fine" && c.FineType == "LateCancel");
        Assert.NotNull(fine);
        Assert.Equal(5000, fine.Amount);
    }

    [Fact]
    public async Task CancelBooking_OutsidePenaltyWindow_NoFine()
    {
        await using var db = await CreateDbAsync();
        var community = await SeedCommunityAsync(db);
        var unit = await SeedUnitAsync(db, community.Id);
        var facility = await SeedFacilityAsync(db, community.Id, false, 60, penaltyHours: 24, lateCancelFine: 5000);

        var service = CreateService(db);
        var start = DateTime.UtcNow.AddHours(25); // In 25 hours (outside 24h)
        var end = start.AddHours(1);
        var booking = await service.CreateBookingAsync(community.Id, facility.Id, unit.Id, Guid.NewGuid(), start, end, null);

        // Act
        await service.CancelBookingAsync(booking.Id, Guid.NewGuid(), "Early enough");

        // Assert
        var fineCount = await db.Charges.CountAsync(c => c.SourceId == booking.Id && c.ChargeKind == "Fine");
        Assert.Equal(0, fineCount);
    }

    [Fact]
    public async Task MarkNoShow_GeneratesFine()
    {
        await using var db = await CreateDbAsync();
        var community = await SeedCommunityAsync(db);
        var unit = await SeedUnitAsync(db, community.Id);
        var facility = await SeedFacilityAsync(db, community.Id, false, 60, noShowFine: 7000);

        var service = CreateService(db);
        var start = DateTime.UtcNow.AddHours(-1); // Started 1h ago
        var end = start.AddHours(1);
        var booking = await service.CreateBookingAsync(community.Id, facility.Id, unit.Id, Guid.NewGuid(), start, end, null);

        // Act
        await service.MarkNoShowAsync(community.Id, facility.Id, booking.Id, Guid.NewGuid());

        // Assert
        var fine = await db.Charges.FirstOrDefaultAsync(c => c.SourceId == booking.Id && c.ChargeKind == "Fine" && c.FineType == "NoShow");
        Assert.NotNull(fine);
        Assert.Equal(7000, fine.Amount);
        
        var updated = await db.Bookings.FindAsync(booking.Id);
        Assert.Equal(BookingStatus.NoShow, updated?.Status);
    }

    [Fact]
    public async Task MarkNoShow_Idempotent()
    {
        await using var db = await CreateDbAsync();
        var community = await SeedCommunityAsync(db);
        var unit = await SeedUnitAsync(db, community.Id);
        var facility = await SeedFacilityAsync(db, community.Id, false, 60, noShowFine: 7000);

        var service = CreateService(db);
        var start = DateTime.UtcNow.AddHours(-1);
        var end = start.AddHours(1);
        var booking = await service.CreateBookingAsync(community.Id, facility.Id, unit.Id, Guid.NewGuid(), start, end, null);

        // Act: Double no-show
        await service.MarkNoShowAsync(community.Id, facility.Id, booking.Id, Guid.NewGuid());
        await service.MarkNoShowAsync(community.Id, facility.Id, booking.Id, Guid.NewGuid());

        // Assert
        var fineCount = await db.Charges.CountAsync(c => c.SourceId == booking.Id && c.ChargeKind == "Fine" && c.FineType == "NoShow");
        Assert.Equal(1, fineCount);
    }

    private static BookingService CreateService(AppDbContext db)
    {
        var repo = new BookingRepository(db);
        var charges = new ChargeRepository(db);
        var blocks = new FacilityBlockRepository(db);
        var uow = new UnitOfWork(db);
        return new BookingService(repo, charges, blocks, uow);
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

    private static async Task<Facility> SeedFacilityAsync(
        AppDbContext db, 
        Guid communityId, 
        bool requiresApproval, 
        int slotMinutes,
        FacilityChargingMode chargingMode = FacilityChargingMode.Free,
        int rent = 0,
        int deposit = 0,
        int penaltyHours = 0,
        int lateCancelFine = 0,
        int noShowFine = 0)
    {
        var facility = new Facility
        {
            CommunityId = communityId,
            Name = "Facility " + Guid.NewGuid().ToString().Substring(0, 5),
            ChargingMode = chargingMode,
            RentAmountClp = rent,
            DepositAmountClp = deposit,
            RequiresApproval = requiresApproval,
            SlotDurationMinutes = slotMinutes,
            CancelPenaltyHours = penaltyHours,
            LateCancelFineAmountClp = lateCancelFine,
            NoShowFineAmountClp = noShowFine,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Facilities.Add(facility);
        await db.SaveChangesAsync();
        return facility;
    }

    [Fact]
    public async Task CompleteBooking_Approved_SetsStatusAndTimestamp()
    {
        await using var db = await CreateDbAsync();
        var service = CreateService(db); 

        var community = await SeedCommunityAsync(db);
        var unit = await SeedUnitAsync(db, community.Id);
        var facility = await SeedFacilityAsync(db, community.Id, requiresApproval: false, slotMinutes: 60);
        
        var start = DateTime.UtcNow;
        var end = start.AddHours(1);

        var booking = new Booking
        {
            CommunityId = community.Id,
            FacilityId = facility.Id,
            UnitId = unit.Id,
            Status = BookingStatus.Approved,
            StartAtUtc = start,
            EndAtUtc = end,
            CreatedByUserId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        await service.CompleteBookingAsync(community.Id, facility.Id, booking.Id, Guid.NewGuid());

        var updated = await db.Bookings.FindAsync(booking.Id);
        Assert.Equal(BookingStatus.Completed, updated?.Status);
        Assert.NotNull(updated?.CompletedAtUtc);
    }

    [Fact]
    public async Task CompleteBooking_NotApproved_ThrowsValidation()
    {
        await using var db = await CreateDbAsync();
        var service = CreateService(db); 

        var community = await SeedCommunityAsync(db);
        var unit = await SeedUnitAsync(db, community.Id);
        var facility = await SeedFacilityAsync(db, community.Id, requiresApproval: true, slotMinutes: 60);

        var start = DateTime.UtcNow;
        var end = start.AddHours(1);
        
        var booking = new Booking
        {
            CommunityId = community.Id,
            FacilityId = facility.Id,
            UnitId = unit.Id,
            Status = BookingStatus.PendingApproval,
            StartAtUtc = start,
            EndAtUtc = end,
            CreatedByUserId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ValidationException>(() => 
            service.CompleteBookingAsync(community.Id, facility.Id, booking.Id, Guid.NewGuid()));
    }
}
