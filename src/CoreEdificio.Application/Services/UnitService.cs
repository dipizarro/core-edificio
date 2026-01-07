using CoreEdificio.Application.Common;
using CoreEdificio.Application.Contracts;
using CoreEdificio.Application.Contracts.Bulk;
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
    public async Task<BulkResponse<Unit>> CreateBulkAsync(Guid communityId, CreateUnitsBulkCommand bulk, CancellationToken ct = default)
    {
        if (!await _repo.CommunityExistsAsync(communityId, ct))
            throw new NotFoundException("Community not found.");

        var results = new List<BulkItemResult<Unit>>();
        var unitsToCreate = new List<Unit>();

        if (bulk.Units is null || bulk.Units.Count == 0)
        {
             return new BulkResponse<Unit>(communityId, 0, 0, 0, results);
        }

        // 1. Normalización y validación básica
        var normalizedItems = bulk.Units.Select((cmd, index) =>
        {
            var number = cmd.Number?.Trim() ?? "";
            var isValid = !string.IsNullOrWhiteSpace(number) && cmd.CoefficientPct > 0 && cmd.CoefficientPct <= 100;
            return new { Index = index, Cmd = cmd, Number = number, IsValid = isValid };
        }).ToList();

        // 2. Detección de duplicados en el request
        var duplicatesInRequest = normalizedItems
            .Where(x => x.IsValid)
            .GroupBy(x => x.Number)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.Skip(1)) // Marcar como fallidos los duplicados subsiguientes (o todos?) -> Estrategia: el primero pasa, el resto falla
            .Select(x => x.Index)
            .ToHashSet();
            
        // Mejor estrategia: si hay duplicados en el request, ¿cuál tomamos? 
        // Simple: tomamos el primero que aparece. Los demás son error "Duplicate in request".

        var requestNumbers = normalizedItems.Where(x => x.IsValid).Select(x => x.Number).Distinct().ToHashSet();

        // 3. Consultar existentes en DB
        var existingNumbers = await _repo.GetExistingUnitNumbersAsync(communityId, requestNumbers, ct);

        // 4. Procesar cada item
        var seenNumbers = new HashSet<string>();

        foreach (var item in normalizedItems)
        {
            if (!item.IsValid)
            {
                results.Add(new BulkItemResult<Unit>(item.Index, false, "Invalid data (Number required, Coefficient > 0)", null));
                continue;
            }

            if (seenNumbers.Contains(item.Number))
            {
                results.Add(new BulkItemResult<Unit>(item.Index, false, "Duplicate in request", null));
                continue;
            }
            seenNumbers.Add(item.Number);

            if (existingNumbers.Contains(item.Number))
            {
                results.Add(new BulkItemResult<Unit>(item.Index, false, "Unit number already exists", null));
                continue;
            }

            // Exito
            var unit = new Unit
            {
                CommunityId = communityId,
                Number = item.Number,
                CoefficientPct = item.Cmd.CoefficientPct,
                OwnerName = string.IsNullOrWhiteSpace(item.Cmd.OwnerName) ? null : item.Cmd.OwnerName.Trim(),
                OwnerEmail = string.IsNullOrWhiteSpace(item.Cmd.OwnerEmail) ? null : item.Cmd.OwnerEmail.Trim()
            };

            unitsToCreate.Add(unit);
            results.Add(new BulkItemResult<Unit>(item.Index, true, null, unit));
        }

        if (unitsToCreate.Count > 0)
        {
            await _repo.AddRangeAsync(unitsToCreate, ct);
        }

        var createdCount = results.Count(x => x.Success);
        var failedCount = results.Count(x => !x.Success);

        return new BulkResponse<Unit>(communityId, bulk.Units.Count, createdCount, failedCount, results);
    }
}
