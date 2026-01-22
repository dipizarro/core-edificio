using CoreEdificio.Api.Auth;
using CoreEdificio.Api.Contracts;
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
    private readonly AppDbContext _db;

    public FacilitiesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<FacilityDto>), 200)]
    public async Task<IActionResult> List(Guid communityId, CancellationToken ct)
    {
        var facilities = await _db.Facilities
            .Where(f => f.CommunityId == communityId)
            .OrderBy(f => f.Name)
            .Select(f => ToDto(f))
            .ToListAsync(ct);

        return Ok(facilities);
    }

    [HttpGet("{facilityId:guid}")]
    [ProducesResponseType(typeof(FacilityDto), 200)]
    public async Task<IActionResult> GetById(Guid communityId, Guid facilityId, CancellationToken ct)
    {
        var facility = await _db.Facilities
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == facilityId && f.CommunityId == communityId, ct);

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
    [ProducesResponseType(typeof(FacilityDto), 201)]
    public async Task<IActionResult> Create(Guid communityId, CreateFacilityRequest request, CancellationToken ct)
    {
        if (!await _db.Communities.AnyAsync(c => c.Id == communityId, ct))
            return NotFound($"Community {communityId} not found.");

        if (!TryParseChargingMode(request.ChargingMode, out var chargingMode))
            return BadRequest("ChargingMode must be one of: Free, Paid, Deposit, PaidAndDeposit.");

        var validationError = ValidateRequest(request.Name, request.SlotDurationMinutes, chargingMode, request.RentAmountClp, request.DepositAmountClp);
        if (validationError is not null)
            return BadRequest(validationError);

        var facility = new Facility
        {
            CommunityId = communityId,
            Name = request.Name.Trim(),
            Description = request.Description,
            Capacity = request.Capacity,
            ChargingMode = chargingMode,
            RentAmountClp = request.RentAmountClp,
            DepositAmountClp = request.DepositAmountClp,
            RequiresApproval = request.RequiresApproval,
            SlotDurationMinutes = request.SlotDurationMinutes,
            MaxHoursPerBooking = request.MaxHoursPerBooking,
            MaxBookingsPerMonthPerUnit = request.MaxBookingsPerMonthPerUnit,
            CreatedAtUtc = DateTime.UtcNow,
            IsActive = true
        };

        _db.Facilities.Add(facility);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { communityId, facilityId = facility.Id }, ToDto(facility));
    }

    [HttpPut("{facilityId:guid}")]
    [ProducesResponseType(typeof(FacilityDto), 200)]
    public async Task<IActionResult> Update(Guid communityId, Guid facilityId, UpdateFacilityRequest request, CancellationToken ct)
    {
        var facility = await _db.Facilities.FirstOrDefaultAsync(f => f.Id == facilityId && f.CommunityId == communityId, ct);
        if (facility is null)
            return NotFound();

        if (!TryParseChargingMode(request.ChargingMode, out var chargingMode))
            return BadRequest("ChargingMode must be one of: Free, Paid, Deposit, PaidAndDeposit.");

        var validationError = ValidateRequest(request.Name, request.SlotDurationMinutes, chargingMode, request.RentAmountClp, request.DepositAmountClp);
        if (validationError is not null)
            return BadRequest(validationError);

        facility.Name = request.Name.Trim();
        facility.Description = request.Description;
        facility.Capacity = request.Capacity;
        facility.ChargingMode = chargingMode;
        facility.RentAmountClp = request.RentAmountClp;
        facility.DepositAmountClp = request.DepositAmountClp;
        facility.RequiresApproval = request.RequiresApproval;
        facility.SlotDurationMinutes = request.SlotDurationMinutes;
        facility.MaxHoursPerBooking = request.MaxHoursPerBooking;
        facility.MaxBookingsPerMonthPerUnit = request.MaxBookingsPerMonthPerUnit;

        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(facility));
    }

    [HttpPost("{facilityId:guid}/deactivate")]
    [ProducesResponseType(typeof(FacilityDto), 200)]
    public async Task<IActionResult> Deactivate(Guid communityId, Guid facilityId, CancellationToken ct)
    {
        var facility = await _db.Facilities.FirstOrDefaultAsync(f => f.Id == facilityId && f.CommunityId == communityId, ct);
        if (facility is null)
            return NotFound();

        if (!facility.IsActive)
            return Ok(ToDto(facility));

        facility.IsActive = false;
        await _db.SaveChangesAsync(ct);

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
            facility.MaxBookingsPerMonthPerUnit);
    }

    private static bool TryParseChargingMode(string chargingMode, out FacilityChargingMode mode)
    {
        mode = default;
        if (string.IsNullOrWhiteSpace(chargingMode))
            return false;

        if (!Enum.TryParse(chargingMode, true, out FacilityChargingMode parsed))
            return false;

        if (!Enum.IsDefined(parsed))
            return false;

        mode = parsed;
        return true;
    }

    private static string? ValidateRequest(string name, int slotDurationMinutes, FacilityChargingMode chargingMode, int rentAmountClp, int depositAmountClp)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Name is required.";

        if (slotDurationMinutes <= 0)
            return "SlotDurationMinutes must be greater than zero.";

        var requiresRent = chargingMode is FacilityChargingMode.Paid or FacilityChargingMode.PaidAndDeposit;
        var requiresDeposit = chargingMode is FacilityChargingMode.Deposit or FacilityChargingMode.PaidAndDeposit;

        if (chargingMode == FacilityChargingMode.Free)
        {
            if (rentAmountClp != 0 || depositAmountClp != 0)
                return "Free facilities must have RentAmountClp and DepositAmountClp set to 0.";
        }

        if (requiresRent && rentAmountClp <= 0)
            return "RentAmountClp must be greater than zero for paid facilities.";

        if (requiresDeposit && depositAmountClp <= 0)
            return "DepositAmountClp must be greater than zero for deposit facilities.";

        return null;
    }
}
