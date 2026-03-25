using CoreEdificio.Application.Common;
using CoreEdificio.Application.Interfaces;
using CoreEdificio.Application.Interfaces.Billing;
using CoreEdificio.Domain.Entities;
using CoreEdificio.Domain.Entities.Billing;

namespace CoreEdificio.Application.Services;

/// <summary>
/// Servicio responsable de gestionar el ciclo de vida de las reservas de instalaciones.
/// </summary>
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

    /// <summary>
    /// Registra una nueva reserva validando solapamientos temporales y cruces con mantenciones.
    /// Ejecuta cobros inmediatos de forma transaccional si la instalación no requiere aprobación.
    /// </summary>
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
            throw new ValidationException("La fecha de inicio debe ser anterior a la fecha de finalización.");

        var facility = await _repo.GetFacilityAsync(communityId, facilityId, ct);
        if (facility is null)
            throw new NotFoundException("Instalación no encontrada.");

        if (!facility.IsActive)
            throw new ValidationException("La instalación se encuentra inactiva.");

        if (facility.SlotDurationMinutes <= 0)
            throw new ValidationException("La duración del bloque configurado para esta instalación es inválida.");

        var durationMinutes = (endUtc - startUtc).TotalMinutes;
        if (durationMinutes <= 0 || durationMinutes % facility.SlotDurationMinutes != 0)
            throw new ValidationException("La duración de la reserva debe ser un múltiplo exacto de los bloques configurados para la instalación.");

        if (!await _repo.UnitExistsAsync(communityId, unitId, ct))
            throw new NotFoundException("La unidad no existe en esta comunidad.");

        // Bloqueos por mantención
        var activeBlocks = await _blocks.GetActiveBlocksInRangeAsync(facilityId, startUtc, endUtc, ct);
        if (activeBlocks.Any())
            throw new ValidationException("La instalación se encuentra bloqueada por mantención u otra razón administrativa para las fechas seleccionadas.");

        // Solapamiento
        if (await _repo.HasOverlapAsync(facilityId, startUtc, endUtc, ct))
            throw new ConflictException("La reserva se cruza con otra previamente agendada.");

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

        // Persistencia Transaccional: Si hay un cobro (por auto-aprobación) y falla, tampoco se guarda la reserva.
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

    /// <summary>
    /// Aprueba manualmente una reserva en estado pendiente, gatillando automáticamente el cobro si corresponde.
    /// </summary>
    public async Task ApproveBookingAsync(Guid communityId, Guid facilityId, Guid bookingId, Guid userId, CancellationToken ct = default)
    {
        var booking = await _repo.GetByIdAsync(bookingId, ct);
        if (booking is null || booking.CommunityId != communityId || booking.FacilityId != facilityId)
            throw new NotFoundException("Reserva no encontrada.");

        if (booking.Status != BookingStatus.PendingApproval)
            throw new ValidationException("Solamente reservas en estado pendiente pueden ser aprobadas.");

        var facility = await _repo.GetFacilityAsync(communityId, facilityId, ct);
        if (facility is null) throw new NotFoundException("Instalación no encontrada.");

        booking.Status = BookingStatus.Approved;
        booking.ApprovedAtUtc = DateTime.UtcNow;
        booking.ApprovedByUserId = userId;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _repo.UpdateAsync(booking, token);
            await GenerateChargesForBookingAsync(booking, facility, token);
        }, ct);
    }

    /// <summary>
    /// Rechaza una reserva proporcionando un motivo de declinación.
    /// </summary>
    public async Task RejectBookingAsync(Guid communityId, Guid facilityId, Guid bookingId, Guid userId, string reason, CancellationToken ct = default)
    {
        var booking = await _repo.GetByIdAsync(bookingId, ct);
        if (booking is null || booking.CommunityId != communityId || booking.FacilityId != facilityId)
            throw new NotFoundException("Reserva no encontrada.");

        if (booking.Status != BookingStatus.PendingApproval)
            throw new ValidationException("Solamente reservas en estado pendiente pueden ser rechazadas.");

        booking.Status = BookingStatus.Rejected;
        booking.RejectReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        booking.ApprovedAtUtc = DateTime.UtcNow;
        booking.ApprovedByUserId = userId;

        await _repo.UpdateAsync(booking, ct);
    }

    /// <summary>
    /// Cancela una reserva validando si aplica una multa por cancelación tardía según las reglas de la instalación.
    /// </summary>
    public async Task CancelBookingAsync(Guid bookingId, Guid userId, string? reason, CancellationToken ct = default)
    {
        var booking = await _repo.GetByIdAsync(bookingId, ct);
        if (booking is null) throw new NotFoundException("Reserva no encontrada.");

        if (booking.Status == BookingStatus.Cancelled) return;

        if (booking.Status != BookingStatus.PendingApproval && booking.Status != BookingStatus.Approved)
            throw new ValidationException($"No es posible cancelar una reserva con estado {booking.Status}.");

        var oldStatus = booking.Status;
        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAtUtc = DateTime.UtcNow;
        booking.CancelledByUserId = userId;
        booking.CancelReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _repo.UpdateAsync(booking, token);

            // Multa por Cancelación Tardía
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

    /// <summary>
    /// Marca un 'No-Show' para el residente, gatillando de inmediato una multa si la instalación lo contempla en su configuración.
    /// </summary>
    public async Task MarkNoShowAsync(Guid communityId, Guid facilityId, Guid bookingId, Guid userId, CancellationToken ct = default)
    {
        var booking = await _repo.GetByIdAsync(bookingId, ct);
        if (booking is null || booking.CommunityId != communityId || booking.FacilityId != facilityId)
            throw new NotFoundException("Reserva no encontrada.");

        if (booking.Status == BookingStatus.NoShow) return;

        if (booking.Status != BookingStatus.Approved)
            throw new ValidationException("Solo reservas aprobadas pueden marcarse como inasistencia (No-Show).");

        if (booking.StartAtUtc > DateTime.UtcNow)
            throw new ValidationException("No se puede marcar inasistencia antes de que inicie la hora de reserva.");

        booking.Status = BookingStatus.NoShow;

        var facility = await _repo.GetFacilityAsync(communityId, facilityId, ct);
        if (facility is null) throw new NotFoundException("Instalación no encontrada.");

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _repo.UpdateAsync(booking, token);

            if (facility.NoShowFineAmountClp > 0)
            {
                await GenerateFineAsync(booking, facility, "NoShow", facility.NoShowFineAmountClp, token);
            }
        }, ct);
    }

    /// <summary>
    /// Método interno de apoyo para generar los cargos asociados (Arriendo/Garantía) tras la aprobación de la Reserva.
    /// </summary>
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
        var label = fineType == "LateCancel" ? "cancelación tardía" : "inasistencia";

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

    /// <summary>
    /// Finaliza una reserva y la marca como completada de forma exitosa logrando cerrar el ciclo de vida administrativamente.
    /// </summary>
    public async Task CompleteBookingAsync(Guid communityId, Guid facilityId, Guid bookingId, Guid userId, CancellationToken ct = default)
    {
        var booking = await _repo.GetByIdAsync(bookingId, ct);
        if (booking is null || booking.CommunityId != communityId || booking.FacilityId != facilityId)
            throw new NotFoundException("Reserva no encontrada.");

        if (booking.Status == BookingStatus.Completed) return;

        if (booking.Status != BookingStatus.Approved)
            throw new ValidationException("Solo reservas aprobadas pueden finalizarse exitosamente.");

        booking.Status = BookingStatus.Completed;
        booking.CompletedAtUtc = DateTime.UtcNow;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _repo.UpdateAsync(booking, token);
        }, ct);
    }
}
