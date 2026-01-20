using CoreEdificio.Application.Common;
using CoreEdificio.Application.Interfaces;
using CoreEdificio.Domain.Entities;

namespace CoreEdificio.Application.Services;

public class BookingService
{
    private readonly IBookingRepository _repo;

    public BookingService(IBookingRepository repo)
    {
        _repo = repo;
    }

    public async Task<Booking> CreateBookingAsync(
        Guid communityId,
        Guid facilityId,
        Guid unitId,
        Guid userId,
        DateTime startUtc,
        DateTime endUtc,
        string? notes,
        CancellationToken ct = default)
    {
        if (startUtc >= endUtc)
            throw new ValidationException("Start time must be before end time.");

        var facility = await _repo.GetFacilityAsync(communityId, facilityId, ct);
        if (facility is null)
            throw new NotFoundException("Facility not found.");

        if (!facility.IsActive)
            throw new ValidationException("Facility is inactive.");

        if (facility.SlotDurationMinutes <= 0)
            throw new ValidationException("Facility slot duration is invalid.");

        var durationMinutes = (endUtc - startUtc).TotalMinutes;
        if (durationMinutes <= 0 || durationMinutes % facility.SlotDurationMinutes != 0)
            throw new ValidationException("Booking duration must be a multiple of the facility slot duration.");

        if (!await _repo.UnitExistsAsync(communityId, unitId, ct))
            throw new NotFoundException("Unit not found in community.");

        if (await _repo.HasOverlapAsync(facilityId, startUtc, endUtc, ct))
            throw new ConflictException("Booking overlaps with an existing reservation.");

        var status = facility.RequiresApproval ? BookingStatus.PendingApproval : BookingStatus.Approved;

        var booking = new Booking
        {
            CommunityId = communityId,
            FacilityId = facilityId,
            UnitId = unitId,
            CreatedByUserId = userId,
            StartAtUtc = startUtc,
            EndAtUtc = endUtc,
            Status = status,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        await _repo.AddAsync(booking, ct);
        return booking;
    }
}
