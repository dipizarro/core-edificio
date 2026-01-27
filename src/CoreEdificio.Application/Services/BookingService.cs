using CoreEdificio.Application.Common;
using CoreEdificio.Application.Interfaces;
using CoreEdificio.Application.Interfaces.Billing;
using CoreEdificio.Domain.Entities;
using CoreEdificio.Domain.Entities.Billing;

namespace CoreEdificio.Application.Services;

public class BookingService
{
    private readonly IBookingRepository _repo;
    private readonly IChargeRepository _charges;
    private readonly IFacilityBlockRepository _blocks;
    private readonly IUnitOfWork _uow;

    public BookingService(IBookingRepository repo, IChargeRepository charges, IFacilityBlockRepository blocks, IUnitOfWork uow)
    {
        _repo = repo;
        _charges = charges;
        _blocks = blocks;
        _uow = uow;
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

        // Check for active blocks
        var activeBlocks = await _blocks.GetActiveBlocksInRangeAsync(facilityId, startUtc, endUtc, ct);
        if (activeBlocks.Any())
            throw new ValidationException("Facility is blocked for the selected time range.");

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

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _repo.AddAsync(booking, token);

            if (booking.Status == BookingStatus.Approved)
            {
                await GenerateChargesForBookingAsync(booking, facility, token);
            }
        }, ct);

        return booking;
    }

    public async Task ApproveBookingAsync(Guid communityId, Guid facilityId, Guid bookingId, Guid userId, CancellationToken ct = default)
    {
        var booking = await _repo.GetByIdAsync(bookingId, ct);
        if (booking is null || booking.CommunityId != communityId || booking.FacilityId != facilityId)
            throw new NotFoundException("Booking not found.");

        if (booking.Status != BookingStatus.PendingApproval)
            throw new ValidationException("Only pending bookings can be approved.");

        var facility = await _repo.GetFacilityAsync(communityId, facilityId, ct);
        if (facility is null) throw new NotFoundException("Facility not found.");

        booking.Status = BookingStatus.Approved;
        booking.ApprovedAtUtc = DateTime.UtcNow;
        booking.ApprovedByUserId = userId;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _repo.UpdateAsync(booking, token);
            await GenerateChargesForBookingAsync(booking, facility, token);
        }, ct);
    }

    public async Task RejectBookingAsync(Guid communityId, Guid facilityId, Guid bookingId, Guid userId, string reason, CancellationToken ct = default)
    {
        var booking = await _repo.GetByIdAsync(bookingId, ct);
        if (booking is null || booking.CommunityId != communityId || booking.FacilityId != facilityId)
            throw new NotFoundException("Booking not found.");

        if (booking.Status != BookingStatus.PendingApproval)
            throw new ValidationException("Only pending bookings can be rejected.");

        booking.Status = BookingStatus.Rejected;
        booking.RejectReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        booking.ApprovedAtUtc = DateTime.UtcNow;
        booking.ApprovedByUserId = userId;

        await _repo.UpdateAsync(booking, ct);
    }

    public async Task CancelBookingAsync(Guid bookingId, Guid userId, string? reason, CancellationToken ct = default)
    {
        var booking = await _repo.GetByIdAsync(bookingId, ct);
        if (booking is null) throw new NotFoundException("Booking not found.");

        if (booking.Status == BookingStatus.Cancelled) return;

        if (booking.Status != BookingStatus.PendingApproval && booking.Status != BookingStatus.Approved)
            throw new ValidationException($"Cannot cancel booking with status {booking.Status}.");

        var oldStatus = booking.Status;
        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAtUtc = DateTime.UtcNow;
        booking.CancelledByUserId = userId;
        booking.CancelReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _repo.UpdateAsync(booking, token);

            if (oldStatus == BookingStatus.Approved)
            {
                var facility = await _repo.GetFacilityAsync(booking.CommunityId, booking.FacilityId, token);
                if (facility is { CancelPenaltyHours: > 0, LateCancelFineAmountClp: > 0 })
                {
                    var hoursToStart = (booking.StartAtUtc - DateTime.UtcNow).TotalHours;
                    if (hoursToStart >= 0 && hoursToStart < facility.CancelPenaltyHours)
                    {
                        await GenerateFineAsync(booking, facility, "LateCancel", facility.LateCancelFineAmountClp, token);
                    }
                }
            }
        }, ct);
    }

    public async Task MarkNoShowAsync(Guid communityId, Guid facilityId, Guid bookingId, Guid userId, CancellationToken ct = default)
    {
        var booking = await _repo.GetByIdAsync(bookingId, ct);
        if (booking is null || booking.CommunityId != communityId || booking.FacilityId != facilityId)
            throw new NotFoundException("Booking not found.");

        if (booking.Status == BookingStatus.NoShow) return;

        if (booking.Status != BookingStatus.Approved)
            throw new ValidationException("Only approved bookings can be marked as No-Show.");

        if (booking.StartAtUtc > DateTime.UtcNow)
            throw new ValidationException("Cannot mark as No-Show before the booking starts.");

        booking.Status = BookingStatus.NoShow;

        var facility = await _repo.GetFacilityAsync(communityId, facilityId, ct);
        if (facility is null) throw new NotFoundException("Facility not found.");

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _repo.UpdateAsync(booking, token);

            if (facility.NoShowFineAmountClp > 0)
            {
                await GenerateFineAsync(booking, facility, "NoShow", facility.NoShowFineAmountClp, token);
            }
        }, ct);
    }

    public async Task GenerateChargesForBookingAsync(Booking booking, Facility facility, CancellationToken ct)
    {
        if (booking.Status != BookingStatus.Approved) return;

        var charges = new List<Charge>();
        var period = booking.StartAtUtc.ToString("yyyy-MM");
        var timeStr = $"{booking.StartAtUtc:yyyy-MM-dd HH:mm}-{booking.EndAtUtc:HH:mm}";

        var hasRent = facility.ChargingMode is FacilityChargingMode.Paid or FacilityChargingMode.PaidAndDeposit;
        var hasDeposit = facility.ChargingMode is FacilityChargingMode.Deposit or FacilityChargingMode.PaidAndDeposit;

        if (hasRent)
        {
            var alreadyExists = await _charges.ExistsAsync("FacilityBooking", booking.Id, "Rent", null, ct);
            if (!alreadyExists)
            {
                charges.Add(new Charge
                {
                    CommunityId = booking.CommunityId,
                    UnitId = booking.UnitId,
                    Amount = facility.RentAmountClp,
                    Description = $"Arriendo {facility.Name} {timeStr}",
                    Period = period,
                    SourceType = "FacilityBooking",
                    SourceId = booking.Id,
                    SourceRef = facility.Name,
                    ChargeKind = "Rent"
                });
            }
        }

        if (hasDeposit)
        {
            var alreadyExists = await _charges.ExistsAsync("FacilityBooking", booking.Id, "Deposit", null, ct);
            if (!alreadyExists)
            {
                charges.Add(new Charge
                {
                    CommunityId = booking.CommunityId,
                    UnitId = booking.UnitId,
                    Amount = facility.DepositAmountClp,
                    Description = $"Garantía {facility.Name} {timeStr}",
                    Period = period,
                    SourceType = "FacilityBooking",
                    SourceId = booking.Id,
                    SourceRef = facility.Name,
                    ChargeKind = "Deposit"
                });
            }
        }

        if (charges.Count > 0)
        {
            await _charges.AddRangeAsync(charges, ct);
        }
    }

    private async Task GenerateFineAsync(Booking booking, Facility facility, string fineType, int amount, CancellationToken ct)
    {
        var alreadyExists = await _charges.ExistsAsync("FacilityBooking", booking.Id, "Fine", fineType, ct);
        if (alreadyExists) return;

        var period = booking.StartAtUtc.ToString("yyyy-MM");
        var timeStr = $"{booking.StartAtUtc:yyyy-MM-dd HH:mm}-{booking.EndAtUtc:HH:mm}";
        var label = fineType == "LateCancel" ? "cancelación tardía" : "no-show";

        var charge = new Charge
        {
            CommunityId = booking.CommunityId,
            UnitId = booking.UnitId,
            Amount = amount,
            Description = $"Multa {label} {facility.Name} {timeStr}",
            Period = period,
            SourceType = "FacilityBooking",
            SourceId = booking.Id,
            SourceRef = facility.Name,
            ChargeKind = "Fine",
            FineType = fineType
        };

        await _charges.AddAsync(charge, ct);
    }

    public async Task CompleteBookingAsync(Guid communityId, Guid facilityId, Guid bookingId, Guid userId, CancellationToken ct = default)
    {
        var booking = await _repo.GetByIdAsync(bookingId, ct);
        if (booking is null || booking.CommunityId != communityId || booking.FacilityId != facilityId)
            throw new NotFoundException("Booking not found.");

        if (booking.Status == BookingStatus.Completed) return;

        if (booking.Status != BookingStatus.Approved)
            throw new ValidationException("Only approved bookings can be completed.");

        booking.Status = BookingStatus.Completed;
        booking.CompletedAtUtc = DateTime.UtcNow;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _repo.UpdateAsync(booking, token);
        }, ct);
    }
}
