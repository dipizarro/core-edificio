using CoreEdificio.Api.Auth;
using CoreEdificio.Api.Contracts;
using CoreEdificio.Application.Common;
using CoreEdificio.Application.Services;
using CoreEdificio.Domain.Entities;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Api.Controllers;

[ApiController]
public class BookingsController : ControllerBase
{
    private readonly BookingService _service;
    private readonly AppDbContext _db;

    public BookingsController(BookingService service, AppDbContext db)
    {
        _service = service;
        _db = db;
    }

    /// <summary>
    /// Crea una solicitud de reserva para una instalación, validando solapamientos y normativas vigentes.
    /// </summary>
    [HttpPost("api/communities/{communityId:guid}/facilities/{facilityId:guid}/bookings")]
    [Authorize(Roles = "Committee,Admin,Resident")]
    [Authorize(Policy = AuthPolicies.CommunityScope)]
    [ProducesResponseType(typeof(BookingDto), 201)]
    public async Task<IActionResult> Create(Guid communityId, Guid facilityId, CreateBookingRequest request, CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);
        var unitId = request.UnitId;

        if (User.IsInRole("Resident"))
        {
            var claimUnitId = UserContext.GetUnitId(User);
            if (claimUnitId is null)
                return Forbid();

            if (request.UnitId != Guid.Empty && request.UnitId != claimUnitId.Value)
                return Forbid();

            unitId = claimUnitId.Value;
        }

        var booking = await _service.CreateBookingAsync(
            communityId,
            facilityId,
            unitId,
            userId,
            request.StartAt,
            request.EndAt,
            request.Notes,
            ct);

        return Created($"/api/bookings/{booking.Id}", ToDto(booking));
    }

    /// <summary>
    /// Consulta el detalle extendido de una reserva específica.
    /// </summary>
    [HttpGet("api/bookings/{bookingId}")]
    [Authorize(Roles = "Committee,Admin,Resident")]
    public async Task<IActionResult> GetById(Guid bookingId, CancellationToken ct)
    {
        var booking = await _db.Bookings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == bookingId, ct);
        if (booking is null) return NotFound();

        var userId = UserContext.GetUserId(User);

        if (User.IsInRole("Resident"))
        {
            var unitId = UserContext.GetUnitId(User);
            if (unitId is null) return Forbid();
            if (booking.UnitId != unitId.Value) return Forbid();
        }
        else
        {
            var scopedCommunityId = UserContext.GetCommunityId(User);
            if (scopedCommunityId.HasValue && booking.CommunityId != scopedCommunityId.Value)
                return Forbid();
        }

        var facilityName = await _db.Facilities
            .Where(f => f.Id == booking.FacilityId)
            .Select(f => f.Name)
            .FirstOrDefaultAsync(ct) ?? "Unknown";

        var unitNumber = await _db.Units
            .Where(u => u.Id == booking.UnitId)
            .Select(u => u.Number)
            .FirstOrDefaultAsync(ct) ?? "Unknown";

        var charges = await _db.Charges
            .AsNoTracking()
            .Where(c => c.SourceType == "FacilityBooking" && c.SourceId == booking.Id)
            .Select(c => new BookingChargeDto(
                c.Id,
                c.ChargeKind,
                c.Description,
                (int)c.Amount,
                c.Period,
                false
            ))
            .ToListAsync(ct);

        var detail = new BookingDetailDto(
            booking.Id,
            booking.CommunityId,
            booking.FacilityId,
            facilityName,
            booking.UnitId,
            unitNumber,
            booking.StartAtUtc,
            booking.EndAtUtc,
            booking.Status.ToString(),
            false,
            booking.Notes,
            booking.CreatedAtUtc,
            booking.ApprovedAtUtc,
            booking.RejectReason,
            booking.CancelledAtUtc,
            booking.CancelReason,
            booking.CompletedAtUtc,
            charges
        );
        
        var facilityReq = await _db.Facilities.Where(f => f.Id == booking.FacilityId).Select(f => f.RequiresApproval).FirstOrDefaultAsync(ct);
        detail = detail with { RequiresApproval = facilityReq };

        return Ok(detail);
    }

    /// <summary>
    /// Lista el historial de reservas para una instalación particular.
    /// </summary>
    [HttpGet("api/communities/{communityId:guid}/facilities/{facilityId:guid}/bookings")]
    [Authorize(Roles = "Committee,Admin,Resident")]
    [Authorize(Policy = AuthPolicies.CommunityScope)]
    [ProducesResponseType(typeof(List<BookingDto>), 200)]
    public async Task<IActionResult> ListFacility(
        Guid communityId,
        Guid facilityId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var query = _db.Bookings
            .AsNoTracking()
            .Where(b => b.CommunityId == communityId && b.FacilityId == facilityId);

        if (from.HasValue)
            query = query.Where(b => b.EndAtUtc > from.Value);

        if (to.HasValue)
            query = query.Where(b => b.StartAtUtc < to.Value);

        var bookings = await query
            .OrderBy(b => b.StartAtUtc)
            .Select(b => ToDto(b))
            .ToListAsync(ct);

        return Ok(bookings);
    }

    /// <summary>
    /// Lista de manera global las reservas efectuadas a lo largo de toda una comunidad. (Vista Administrador)
    /// </summary>
    [HttpGet("api/communities/{communityId:guid}/bookings")]
    [Authorize(Roles = "Committee,Admin")]
    [Authorize(Policy = AuthPolicies.CommunityScope)]
    [ProducesResponseType(typeof(List<BookingListDto>), 200)]
    public async Task<IActionResult> ListCommunity(
        Guid communityId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? facilityId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var query = from b in _db.Bookings.AsNoTracking()
                    join f in _db.Facilities on b.FacilityId equals f.Id
                    join u in _db.Units on b.UnitId equals u.Id
                    where b.CommunityId == communityId
                    select new { b, FacilityName = f.Name, UnitNumber = u.Number };

        if (facilityId.HasValue)
            query = query.Where(x => x.b.FacilityId == facilityId.Value);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BookingStatus>(status, true, out var statusEnum))
            query = query.Where(x => x.b.Status == statusEnum);

        if (from.HasValue)
            query = query.Where(x => x.b.EndAtUtc > from.Value);

        if (to.HasValue)
            query = query.Where(x => x.b.StartAtUtc < to.Value);

        var items = await query
            .OrderByDescending(x => x.b.StartAtUtc)
            .Select(x => new BookingListDto(
                x.b.Id,
                x.b.FacilityId,
                x.FacilityName,
                x.b.UnitId,
                x.UnitNumber,
                x.b.StartAtUtc,
                x.b.EndAtUtc,
                x.b.Status.ToString()
            ))
            .ToListAsync(ct);

        return Ok(items);
    }

    /// <summary>
    /// Lista el historial de reservas efectuadas por el usuario autenticado (Mis Reservas).
    /// </summary>
    [HttpGet("api/bookings/my")]
    [Authorize(Roles = "Committee,Admin,Resident")]
    [ProducesResponseType(typeof(List<BookingListDto>), 200)]
    public async Task<IActionResult> ListMine(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? communityId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var query = from b in _db.Bookings.AsNoTracking()
                    join f in _db.Facilities on b.FacilityId equals f.Id
                    join u in _db.Units on b.UnitId equals u.Id
                    select new { b, FacilityName = f.Name, UnitNumber = u.Number };

        if (User.IsInRole("Resident"))
        {
            var unitId = UserContext.GetUnitId(User);
            if (unitId is null) return Forbid();
            query = query.Where(x => x.b.UnitId == unitId.Value);
        }
        else
        {
            var scopedCommunityId = communityId ?? UserContext.GetCommunityId(User);
            if (scopedCommunityId is null)
                throw new ValidationException("communityId is required for this request.");

            query = query.Where(x => x.b.CommunityId == scopedCommunityId.Value);
        }

        if (from.HasValue)
            query = query.Where(x => x.b.EndAtUtc > from.Value);

        if (to.HasValue)
            query = query.Where(x => x.b.StartAtUtc < to.Value);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BookingStatus>(status, true, out var statusEnum))
            query = query.Where(x => x.b.Status == statusEnum);

        var items = await query
            .OrderByDescending(x => x.b.StartAtUtc)
            .Select(x => new BookingListDto(
                x.b.Id,
                x.b.FacilityId,
                x.FacilityName,
                x.b.UnitId,
                x.UnitNumber,
                x.b.StartAtUtc,
                x.b.EndAtUtc,
                x.b.Status.ToString()
            ))
            .ToListAsync(ct);

        return Ok(items);
    }

    /// <summary>
    /// Aprueba manualmente (Comité/Administración) una reserva pendiente.
    /// </summary>
    [HttpPost("api/communities/{communityId:guid}/facilities/{facilityId:guid}/bookings/{bookingId:guid}/approve")]
    [Authorize(Roles = "Committee,Admin")]
    [Authorize(Policy = AuthPolicies.CommunityScope)]
    public async Task<IActionResult> Approve(Guid communityId, Guid facilityId, Guid bookingId, CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);
        await _service.ApproveBookingAsync(communityId, facilityId, bookingId, userId, ct);
        return NoContent();
    }

    /// <summary>
    /// Rechaza una reserva pendiente asignando un motivo explícito a la acción.
    /// </summary>
    [HttpPost("api/communities/{communityId:guid}/facilities/{facilityId:guid}/bookings/{bookingId:guid}/reject")]
    [Authorize(Roles = "Committee,Admin")]
    [Authorize(Policy = AuthPolicies.CommunityScope)]
    public async Task<IActionResult> Reject(Guid communityId, Guid facilityId, Guid bookingId, RejectBookingRequest request, CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);
        await _service.RejectBookingAsync(communityId, facilityId, bookingId, userId, request.Reason, ct);
        return NoContent();
    }

    /// <summary>
    /// Cancela una reserva validada calculando y operando penalidades por retrasos de cancelación temprana.
    /// </summary>
    [HttpPost("api/bookings/{bookingId:guid}/cancel")]
    [Authorize(Roles = "Committee,Admin,Resident")]
    public async Task<IActionResult> Cancel(Guid bookingId, CancelBookingRequest request, CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);
        
        if (User.IsInRole("Resident"))
        {
            var unitId = UserContext.GetUnitId(User);
            if (unitId is null) return Forbid();

            var booking = await _db.Bookings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == bookingId, ct);
            if (booking is null) return NotFound();
            if (booking.UnitId != unitId.Value) return Forbid();
        }
        else
        {
            var booking = await _db.Bookings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == bookingId, ct);
            if (booking is null) return NotFound();
            
            var scopedCommunityId = UserContext.GetCommunityId(User);
            if (scopedCommunityId.HasValue && booking.CommunityId != scopedCommunityId.Value)
                return Forbid();
        }

        await _service.CancelBookingAsync(bookingId, userId, request.Reason, ct);
        return NoContent();
    }

    /// <summary>
    /// Marca el evento como No Acudido (No-Show). Gatilla castigos si están configurados en la instalación.
    /// </summary>
    [HttpPost("api/communities/{communityId:guid}/facilities/{facilityId:guid}/bookings/{bookingId:guid}/no-show")]
    [Authorize(Roles = "Committee,Admin")]
    [Authorize(Policy = AuthPolicies.CommunityScope)]
    public async Task<IActionResult> MarkNoShow(Guid communityId, Guid facilityId, Guid bookingId, CancellationToken ct)
    {
        await _service.MarkNoShowAsync(communityId, facilityId, bookingId, UserContext.GetUserId(User), ct);
        return NoContent();
    }

    /// <summary>
    /// Cierra el ciclo de una reserva y la declara Completada satisfactoriamente tras el uso.
    /// </summary>
    [HttpPost("api/bookings/{bookingId:guid}/complete")]
    [Authorize(Roles = "Committee,Admin")]
    public async Task<IActionResult> Complete(Guid bookingId, CancellationToken ct)
    {
        var booking = await _db.Bookings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == bookingId, ct);
        if (booking is null) return NotFound();

        var scopedCommunityId = UserContext.GetCommunityId(User);
        if (scopedCommunityId.HasValue && booking.CommunityId != scopedCommunityId.Value)
            return Forbid();
        
        if (User.IsInRole("Resident"))
        {
             var unitId = UserContext.GetUnitId(User);
             if (unitId is null) return Forbid();
             
             if (booking.UnitId != unitId.Value) return Forbid();

             if (DateTime.UtcNow <= booking.EndAtUtc)
                throw new ValidationException("Cannot complete booking before it ends.");
        }

        await _service.CompleteBookingAsync(booking.CommunityId, booking.FacilityId, bookingId, UserContext.GetUserId(User), ct);
        return NoContent();
    }

    private static BookingDto ToDto(Booking booking)
    {
        return new BookingDto(
            booking.Id,
            booking.FacilityId,
            booking.UnitId,
            booking.StartAtUtc,
            booking.EndAtUtc,
            booking.Status.ToString(),
            booking.Notes);
    }
}
