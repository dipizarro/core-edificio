using CoreEdificio.Application.Common;
using CoreEdificio.Application.Contracts;
using CoreEdificio.Application.Interfaces;
using CoreEdificio.Domain.Entities;

namespace CoreEdificio.Application.Services;

public class UnitService
{
    private readonly IUnitRepository _repo;

    public UnitService(IUnitRepository repo) => _repo = repo;

    public async Task<Unit> CreateAsync(Guid communityId, CreateUnitCommand cmd, CancellationToken ct = default)
    {
        if (!await _repo.CommunityExistsAsync(communityId, ct))
            throw new NotFoundException("Community not found.");

        if (string.IsNullOrWhiteSpace(cmd.Number))
            throw new ValidationException("Unit number is required.");

        if (cmd.CoefficientPct <= 0 || cmd.CoefficientPct > 100)
            throw new ValidationException("CoefficientPct must be > 0 and <= 100.");

        var number = cmd.Number.Trim();

        if (await _repo.UnitNumberExistsAsync(communityId, number, ct))
            throw new ConflictException("Unit number already exists in this community.");

        var unit = new Unit
        {
            CommunityId = communityId,
            Number = number,
            CoefficientPct = cmd.CoefficientPct,
            OwnerName = string.IsNullOrWhiteSpace(cmd.OwnerName) ? null : cmd.OwnerName.Trim(),
            OwnerEmail = string.IsNullOrWhiteSpace(cmd.OwnerEmail) ? null : cmd.OwnerEmail.Trim()
        };

        await _repo.AddAsync(unit, ct);
        return unit;
    }

    public Task<List<Unit>> ListByCommunityAsync(Guid communityId, CancellationToken ct = default)
        => _repo.ListByCommunityAsync(communityId, ct);

    public Task<(int Count, decimal TotalCoefficientPct)> GetCoefficientSummaryAsync(Guid communityId, CancellationToken ct = default)
        => _repo.GetCoefficientSummaryAsync(communityId, ct);
}
