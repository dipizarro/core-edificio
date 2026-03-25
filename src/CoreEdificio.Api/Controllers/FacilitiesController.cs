using CoreEdificio.Api.Auth;
using CoreEdificio.Api.Contracts;
using CoreEdificio.Application.Services;
using CoreEdificio.Domain.Entities;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Api.Controllers;

[Authorize(Roles = "Committee,Admin")]
[Authorize(Policy = AuthPolicies.CommunityScope)]
[ApiController]
[Route("api/communities/{communityId:guid}/facilities")]
public class FacilitiesController : ControllerBase
{
    private readonly FacilityService _service;
    private readonly AppDbContext _db;

    public FacilitiesController(FacilityService service, AppDbContext db)
    {
        _service = service;
        _db = db;
    }

    /// <summary>
    /// Lista de instalaciones disponibles para la comunidad.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<FacilityDto>), 200)]
    public async Task<IActionResult> List(Guid communityId, CancellationToken ct)
    {
        var facilities = await _service.ListAsync(communityId, ct);
        return Ok(facilities.Select(ToDto));
    }

    /// <summary>
    /// Devuelve los datos de una instalación particular.
    /// </summary>
    [HttpGet("{facilityId:guid}")]
    [ProducesResponseType(typeof(FacilityDto), 200)]
    public async Task<IActionResult> GetById(Guid communityId, Guid facilityId, CancellationToken ct)
    {
        var facility = await _service.GetByIdAsync(communityId, facilityId, ct);
        if (facility is null)
            return NotFound();

        return Ok(ToDto(facility));
    }

    [HttpGet("{facilityId:guid}/availability")]
    [Authorize(Roles = "Committee,Admin,Resident")]
    [Authorize(Policy = AuthPolicies.CommunityScope)]
    [ProducesResponseType(typeof(FacilityAvailabilityResponse), 200)]
    public async Task<IActionResult> GetAvailability(Guid communityId, Guid facilityId, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        if (from >= to)
            return BadRequest("Start time (from) must be before end time (to).");

        if ((to - from).TotalDays > 31)
            return BadRequest("Maximum availability range is 31 days.");

        var facility = await _db.Facilities.AsNoTracking().FirstOrDefaultAsync(f => f.Id == facilityId && f.CommunityId == communityId, ct);
        if (facility is null)
            return NotFound("Facility not found.");

        // Query Bookings
        var bookings = await _db.Bookings.AsNoTracking()
            .Where(b => b.CommunityId == communityId && b.FacilityId == facilityId &&
                        (b.Status == BookingStatus.PendingApproval || b.Status == BookingStatus.Approved) &&
                        b.StartAtUtc < to && b.EndAtUtc > from)
            .Join(_db.Units, b => b.UnitId, u => u.Id, (b, u) => new { Booking = b, UnitNumber = u.Number })
            .ToListAsync(ct);

        // Query Blocks
        var blocks = await _db.FacilityBlocks.AsNoTracking()
            .Where(x => x.CommunityId == communityId && x.FacilityId == facilityId && x.IsActive &&
                        x.StartAtUtc < to && x.EndAtUtc > from)
            .ToListAsync(ct);

        var isStaff = User.IsInRole("Committee") || User.IsInRole("Admin");

        var intervals = new List<AvailabilityIntervalDto>();

        foreach (var b in bookings)
        {
            intervals.Add(new AvailabilityIntervalDto(
                Kind: "Booking",
                StartAtUtc: b.Booking.StartAtUtc,
                EndAtUtc: b.Booking.EndAtUtc,
                StatusOrReason: b.Booking.Status.ToString(),
                BookingId: b.Booking.Id,
                BlockId: null,
                UnitNumber: isStaff ? b.UnitNumber : null,
                IsTentative: b.Booking.Status == BookingStatus.PendingApproval
            ));
        }

        foreach (var block in blocks)
        {
            intervals.Add(new AvailabilityIntervalDto(
                Kind: "Block",
                StartAtUtc: block.StartAtUtc,
                EndAtUtc: block.EndAtUtc,
                StatusOrReason: block.Reason,
                BookingId: null,
                BlockId: block.Id,
                UnitNumber: null,
                IsTentative: false
            ));
        }

        var response = new FacilityAvailabilityResponse(
            CommunityId: communityId,
            FacilityId: facilityId,
            FromUtc: from,
            ToUtc: to,
            Intervals: intervals.OrderBy(x => x.StartAtUtc).ToList()
        );

        return Ok(response);
    }

    [HttpGet("{facilityId:guid}/availability/slots")]
    [Authorize(Roles = "Committee,Admin,Resident")]
    [Authorize(Policy = AuthPolicies.CommunityScope)]
    [ProducesResponseType(typeof(FacilityAvailabilitySlotsResponse), 200)]
    public async Task<IActionResult> GetAvailabilitySlots(Guid communityId, Guid facilityId, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        if (from >= to)
            return BadRequest("Start time (from) must be before end time (to).");

        if ((to - from).TotalDays > 14)
            return BadRequest("Maximum availability slots range is 14 days.");

        var facility = await _db.Facilities.AsNoTracking().FirstOrDefaultAsync(f => f.Id == facilityId && f.CommunityId == communityId, ct);
        if (facility is null)
            return NotFound("Facility not found.");

        if (facility.SlotDurationMinutes <= 0)
            return BadRequest("Facility slot duration is invalid.");

        // Query Bookings (Pending/Approved)
        var bookings = await _db.Bookings.AsNoTracking()
            .Where(b => b.CommunityId == communityId && b.FacilityId == facilityId &&
                        (b.Status == BookingStatus.PendingApproval || b.Status == BookingStatus.Approved) &&
                        b.StartAtUtc < to && b.EndAtUtc > from)
            .ToListAsync(ct);

        // Query Blocks
        var blocks = await _db.FacilityBlocks.AsNoTracking()
            .Where(x => x.CommunityId == communityId && x.FacilityId == facilityId && x.IsActive &&
                        x.StartAtUtc < to && x.EndAtUtc > from)
            .ToListAsync(ct);

        var slots = new List<AvailabilitySlotDto>();
        var duration = TimeSpan.FromMinutes(facility.SlotDurationMinutes);
        var current = from;

        while (current < to)
        {
            var slotStart = current;
            var slotEnd = current + duration;
            if (slotEnd > to) slotEnd = to;
            if (slotEnd <= slotStart) break;

            // Classification Priority: Blocked > Booked > Pending > Free
            var block = blocks.FirstOrDefault(x => x.StartAtUtc < slotEnd && x.EndAtUtc > slotStart);
            var booking = bookings.FirstOrDefault(x => x.StartAtUtc < slotEnd && x.EndAtUtc > slotStart);

            string status;
            string? reasonOrStatus = null;
            Guid? bId = null;
            Guid? blockId = null;

            if (block != null)
            {
                status = "Blocked";
                reasonOrStatus = block.Reason;
                blockId = block.Id;
            }
            else if (booking != null && booking.Status == BookingStatus.Approved)
            {
                status = "Booked";
                reasonOrStatus = "Approved";
                bId = booking.Id;
            }
            else if (booking != null && booking.Status == BookingStatus.PendingApproval)
            {
                status = "Pending";
                reasonOrStatus = "PendingApproval";
                bId = booking.Id;
            }
            else
            {
                status = "Free";
            }

            slots.Add(new AvailabilitySlotDto(slotStart, slotEnd, status, reasonOrStatus, bId, blockId));
            current = slotEnd;
        }

        var response = new FacilityAvailabilitySlotsResponse(
            CommunityId: communityId,
            FacilityId: facilityId,
            FromUtc: from,
            ToUtc: to,
            SlotMinutes: facility.SlotDurationMinutes,
            Slots: slots
        );

        return Ok(response);
    }
    
    /// <summary>
    /// Crea una nueva instalación comunitaria (Quincho, Piscina, Sala Multiuso).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(FacilityDto), 201)]
    public async Task<IActionResult> Create(Guid communityId, CreateFacilityRequest request, CancellationToken ct)
    {
        var dto = new CreateFacilityDto
        {
            Name = request.Name,
            Description = request.Description,
            Capacity = request.Capacity,
            ChargingMode = request.ChargingMode,
            RentAmountClp = request.RentAmountClp,
            DepositAmountClp = request.DepositAmountClp,
            RequiresApproval = request.RequiresApproval,
            SlotDurationMinutes = request.SlotDurationMinutes,
            MaxHoursPerBooking = request.MaxHoursPerBooking,
            MaxBookingsPerMonthPerUnit = request.MaxBookingsPerMonthPerUnit,
            CancelPenaltyHours = request.CancelPenaltyHours,
            LateCancelFineAmountClp = request.LateCancelFineAmountClp,
            NoShowFineAmountClp = request.NoShowFineAmountClp
        };

        var facility = await _service.CreateAsync(communityId, dto, ct);
        return CreatedAtAction(nameof(GetById), new { communityId, facilityId = facility.Id }, ToDto(facility));
    }

    /// <summary>
    /// Actualiza la configuración de una instalación (Cobros, penalidades, horas).
    /// </summary>
    [HttpPut("{facilityId:guid}")]
    [ProducesResponseType(typeof(FacilityDto), 200)]
    public async Task<IActionResult> Update(Guid communityId, Guid facilityId, UpdateFacilityRequest request, CancellationToken ct)
    {
        var dto = new UpdateFacilityDto
        {
            Name = request.Name,
            Description = request.Description,
            Capacity = request.Capacity,
            ChargingMode = request.ChargingMode,
            RentAmountClp = request.RentAmountClp,
            DepositAmountClp = request.DepositAmountClp,
            RequiresApproval = request.RequiresApproval,
            SlotDurationMinutes = request.SlotDurationMinutes,
            MaxHoursPerBooking = request.MaxHoursPerBooking,
            MaxBookingsPerMonthPerUnit = request.MaxBookingsPerMonthPerUnit,
            CancelPenaltyHours = request.CancelPenaltyHours,
            LateCancelFineAmountClp = request.LateCancelFineAmountClp,
            NoShowFineAmountClp = request.NoShowFineAmountClp
        };

        var facility = await _service.UpdateAsync(communityId, facilityId, dto, ct);
        return Ok(ToDto(facility));
    }

    /// <summary>
    /// Desactiva lógicamente una instalación para que no se puedan agendar más reservas.
    /// </summary>
    [HttpPost("{facilityId:guid}/deactivate")]
    [ProducesResponseType(typeof(FacilityDto), 200)]
    public async Task<IActionResult> Deactivate(Guid communityId, Guid facilityId, CancellationToken ct)
    {
        var facility = await _service.DeactivateAsync(communityId, facilityId, ct);
        return Ok(ToDto(facility));
    }

    private static FacilityDto ToDto(Facility facility)
    {
        return new FacilityDto(
            facility.Id,
            facility.CommunityId,
            facility.Name,
            facility.Description,
            facility.IsActive,
            facility.Capacity,
            facility.ChargingMode.ToString(),
            facility.RentAmountClp,
            facility.DepositAmountClp,
            facility.RequiresApproval,
            facility.SlotDurationMinutes,
            facility.MaxHoursPerBooking,
            facility.MaxBookingsPerMonthPerUnit,
            facility.CancelPenaltyHours,
            facility.LateCancelFineAmountClp,
            facility.NoShowFineAmountClp);
    }
}
