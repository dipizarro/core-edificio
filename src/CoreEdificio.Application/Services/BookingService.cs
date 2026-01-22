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

        // Regla: Pendiente o Aprobado -> Cancelado
        if (booking.Status != BookingStatus.PendingApproval && booking.Status != BookingStatus.Approved)
            throw new ValidationException($"Cannot cancel booking with status {booking.Status}.");

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAtUtc = DateTime.UtcNow;
        booking.CancelledByUserId = userId;
        booking.CancelReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        await _repo.UpdateAsync(booking, ct);
    }

    public async Task GenerateChargesForBookingAsync(Booking booking, Facility facility, CancellationToken ct)
    {
        if (booking.Status != BookingStatus.Approved) return;

        var charges = new List<Charge>();
        var period = booking.StartAtUtc.ToString("yyyy-MM");
        var timeStr = $"{booking.StartAtUtc:yyyy-MM-dd HH:mm}-{booking.EndAtUtc:HH:mm}";

        // Idempotencia: Verificar si ya existen cargos para esta booking
        var hasRent = facility.ChargingMode is FacilityChargingMode.Paid or FacilityChargingMode.PaidAndDeposit;
        var hasDeposit = facility.ChargingMode is FacilityChargingMode.Deposit or FacilityChargingMode.PaidAndDeposit;

        if (hasRent)
        {
            var alreadyExists = await _charges.ExistsAsync("FacilityBooking", booking.Id, "Rent", ct);
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
            var alreadyExists = await _charges.ExistsAsync("FacilityBooking", booking.Id, "Deposit", ct);
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
}
