using CoreEdificio.Domain.Entities;

namespace CoreEdificio.Application.Interfaces;

public interface IBookingRepository
{
    Task<Facility?> GetFacilityAsync(Guid communityId, Guid facilityId, CancellationToken ct = default);
    Task<bool> UnitExistsAsync(Guid communityId, Guid unitId, CancellationToken ct = default);
    Task<bool> HasOverlapAsync(Guid facilityId, DateTime startUtc, DateTime endUtc, CancellationToken ct = default);
    Task AddAsync(Booking booking, CancellationToken ct = default);
    
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task UpdateAsync(Booking booking, CancellationToken ct = default);
}
