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

    [HttpGet("api/bookings/my")]
    [Authorize(Roles = "Committee,Admin,Resident")]
    [ProducesResponseType(typeof(List<BookingDto>), 200)]
    public async Task<IActionResult> ListMine(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? communityId,
        CancellationToken ct)
    {
        var query = _db.Bookings.AsNoTracking();

        if (User.IsInRole("Resident"))
        {
            var unitId = UserContext.GetUnitId(User);
            if (unitId is null)
                return Forbid();

            query = query.Where(b => b.UnitId == unitId.Value);
        }
        else
        {
            var scopedCommunityId = communityId ?? UserContext.GetCommunityId(User);
            if (scopedCommunityId is null)
                throw new ValidationException("communityId is required for this request.");

            query = query.Where(b => b.CommunityId == scopedCommunityId.Value);
        }

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
