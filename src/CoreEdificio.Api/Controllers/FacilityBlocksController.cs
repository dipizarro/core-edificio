using CoreEdificio.Api.Auth;
using CoreEdificio.Api.Contracts;
using CoreEdificio.Application.Common;
using CoreEdificio.Application.Interfaces;
using CoreEdificio.Domain.Entities;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Api.Controllers;

[ApiController]
public class FacilityBlocksController : ControllerBase
{
    private readonly IFacilityBlockRepository _blocks;
    private readonly AppDbContext _db;

    public FacilityBlocksController(IFacilityBlockRepository blocks, AppDbContext db)
    {
        _blocks = blocks;
        _db = db;
    }

    [HttpPost("api/communities/{communityId:guid}/facilities/{facilityId:guid}/blocks")]
    [Authorize(Roles = "Committee,Admin")]
    [Authorize(Policy = AuthPolicies.CommunityScope)]
    public async Task<IActionResult> Create(Guid communityId, Guid facilityId, CreateFacilityBlockRequest request, CancellationToken ct)
    {
        if (request.StartAtUtc >= request.EndAtUtc)
            return BadRequest("Start time must be before end time.");

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length < 3)
            return BadRequest("Reason is required (min 3 characters).");

        var facility = await _db.Facilities.AsNoTracking().FirstOrDefaultAsync(f => f.Id == facilityId && f.CommunityId == communityId, ct);
        if (facility is null)
            return NotFound("Facility not found in community.");

        var block = new FacilityBlock
        {
            CommunityId = communityId,
            FacilityId = facilityId,
            StartAtUtc = request.StartAtUtc,
            EndAtUtc = request.EndAtUtc,
            Reason = request.Reason.Trim(),
            CreatedByUserId = UserContext.GetUserId(User),
            CreatedAtUtc = DateTime.UtcNow
        };

        await _blocks.AddAsync(block, ct);

        return Created($"/api/communities/{communityId}/facilities/{facilityId}/blocks/{block.Id}", ToDto(block));
    }

    [HttpGet("api/communities/{communityId:guid}/facilities/{facilityId:guid}/blocks")]
    [Authorize(Roles = "Committee,Admin,Resident")]
    [Authorize(Policy = AuthPolicies.CommunityScope)]
    public async Task<ActionResult<List<FacilityBlockDto>>> List(Guid communityId, Guid facilityId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var query = _db.FacilityBlocks.AsNoTracking()
            .Where(x => x.CommunityId == communityId && x.FacilityId == facilityId && x.IsActive);

        if (from.HasValue)
            query = query.Where(x => x.EndAtUtc > from.Value);
        
        if (to.HasValue)
            query = query.Where(x => x.StartAtUtc < to.Value);

        var blocks = await query.OrderBy(x => x.StartAtUtc).ToListAsync(ct);
        return Ok(blocks.Select(ToDto).ToList());
    }

    [HttpPost("api/communities/{communityId:guid}/facilities/{facilityId:guid}/blocks/{blockId:guid}/deactivate")]
    [Authorize(Roles = "Committee,Admin")]
    [Authorize(Policy = AuthPolicies.CommunityScope)]
    public async Task<IActionResult> Deactivate(Guid communityId, Guid facilityId, Guid blockId, CancellationToken ct)
    {
        var block = await _blocks.GetByIdAsync(blockId, ct);
        if (block is null || block.CommunityId != communityId || block.FacilityId != facilityId)
            return NotFound("Block not found.");

        block.IsActive = false;
        await _blocks.UpdateAsync(block, ct);

        return NoContent();
    }

    private static FacilityBlockDto ToDto(FacilityBlock block)
    {
        return new FacilityBlockDto(
            block.Id,
            block.FacilityId,
            block.StartAtUtc,
            block.EndAtUtc,
            block.Reason,
            block.IsActive,
            block.CreatedAtUtc);
    }
}
