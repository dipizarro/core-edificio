using CoreEdificio.Application.Interfaces;
using CoreEdificio.Domain.Entities;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Repositories;

public class BookingRepository : IBookingRepository
{
    private readonly AppDbContext _db;

    public BookingRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Facility?> GetFacilityAsync(Guid communityId, Guid facilityId, CancellationToken ct = default)
    {
        return _db.Facilities
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == facilityId && f.CommunityId == communityId, ct);
    }

    public Task<bool> UnitExistsAsync(Guid communityId, Guid unitId, CancellationToken ct = default)
    {
        return _db.Units.AnyAsync(u => u.Id == unitId && u.CommunityId == communityId, ct);
    }

    public Task<bool> HasOverlapAsync(Guid facilityId, DateTime startUtc, DateTime endUtc, CancellationToken ct = default)
    {
        return _db.Bookings.AnyAsync(b =>
            b.FacilityId == facilityId &&
            (b.Status == BookingStatus.PendingApproval || b.Status == BookingStatus.Approved) &&
            b.StartAtUtc < endUtc &&
            b.EndAtUtc > startUtc,
            ct);
    }

    public async Task AddAsync(Booking booking, CancellationToken ct = default)
    {
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync(ct);
    }
}
