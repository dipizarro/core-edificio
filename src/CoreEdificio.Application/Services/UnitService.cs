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

        if (string.IsNullOrWhiteSpace(cmd.UnitNumber))
            throw new ValidationException("Unit number is required.");

        if (cmd.CoefficientPct <= 0 || cmd.CoefficientPct > 100)
            throw new ValidationException("CoefficientPct must be > 0 and <= 100.");

        var number = cmd.UnitNumber.Trim();

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

        // Componente por defecto
        unit.Components.Add(new UnitComponent
        {
            CommunityId = communityId,
            UnitId = unit.Id,
            Type = "Department",
            Code = number,
            CoefficientPct = cmd.CoefficientPct
        });

        await _repo.AddAsync(unit, ct);
        return unit;
    }

    public Task<List<Unit>> ListByCommunityAsync(Guid communityId, CancellationToken ct = default)
        => _repo.ListByCommunityAsync(communityId, ct);

    public Task<(int Count, decimal TotalCoefficientPct)> GetCoefficientSummaryAsync(Guid communityId, CancellationToken ct = default)
        => _repo.GetCoefficientSummaryAsync(communityId, ct);
    public async Task<BulkResponse<UnitResponseDto>> CreateBulkAsync(Guid communityId, CreateUnitsBulkCommand bulk, CancellationToken ct = default)
    {
        if (!await _repo.CommunityExistsAsync(communityId, ct))
            throw new NotFoundException("Community not found.");

        var results = new List<BulkItemResult<UnitResponseDto>>();
        var unitsToCreate = new List<Unit>();

        if (bulk.Units is null || bulk.Units.Count == 0)
        {
            return new BulkResponse<UnitResponseDto>(communityId, 0, 0, 0, results);
        }

        var requestNumbers = bulk.Units.Select(x => x.UnitNumber.Trim()).ToHashSet();
        var existingNumbers = await _repo.GetExistingUnitNumbersAsync(communityId, requestNumbers, ct);

        var seenNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var normalizedItems = bulk.Units.Select((cmd, index) => new
        {
            Index = index,
            Cmd = cmd,
            Number = cmd.UnitNumber?.Trim() ?? "",
            IsValid = !string.IsNullOrWhiteSpace(cmd.UnitNumber) && cmd.CoefficientPct > 0
        }).ToList();

        foreach (var item in normalizedItems)
        {
            if (!item.IsValid)
            {
                results.Add(new BulkItemResult<UnitResponseDto>(item.Index, false, "Invalid data (Number required, Coefficient > 0)", null));
                continue;
            }

            if (seenNumbers.Contains(item.Number))
            {
                results.Add(new BulkItemResult<UnitResponseDto>(item.Index, false, "Duplicate in request", null));
                continue;
            }
            seenNumbers.Add(item.Number);

            if (existingNumbers.Contains(item.Number))
            {
                results.Add(new BulkItemResult<UnitResponseDto>(item.Index, false, "Unit number already exists", null));
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

            // Componente por defecto
            unit.Components.Add(new UnitComponent
            {
                CommunityId = communityId,
                UnitId = unit.Id,
                Type = "Department",
                Code = item.Number,
                CoefficientPct = item.Cmd.CoefficientPct
            });

            unitsToCreate.Add(unit);
            results.Add(new BulkItemResult<UnitResponseDto>(item.Index, true, null, MapToResponse(unit)));
        }

        if (unitsToCreate.Count > 0)
        {
            await _repo.AddRangeAsync(unitsToCreate, ct);
        }

        var createdCount = results.Count(x => x.Success);
        var failedCount = results.Count(x => !x.Success);

        return new BulkResponse<UnitResponseDto>(communityId, bulk.Units.Count, createdCount, failedCount, results);
    }

    public async Task<BulkResponse<UnitResponseDto>> CreateBulkWithComponentsAsync(Guid communityId, CreateUnitsWithComponentsBulkCommand bulk, CancellationToken ct = default)
    {
        if (!await _repo.CommunityExistsAsync(communityId, ct))
            throw new NotFoundException("Community not found.");

        var results = new List<BulkItemResult<UnitResponseDto>>();
        var unitsToCreate = new List<Unit>();

        if (bulk.Units is null || bulk.Units.Count == 0)
        {
            return new BulkResponse<UnitResponseDto>(communityId, 0, 0, 0, results);
        }

        // 1. Detección de duplicados en el request (UnitNumber)
        var duplicatesInRequest = bulk.Units
            .GroupBy(x => x.UnitNumber.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var requestNumbers = bulk.Units.Select(x => x.UnitNumber.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet();

        // 2. Consultar existentes en DB
        var existingNumbers = await _repo.GetExistingUnitNumbersAsync(communityId, requestNumbers, ct);

        // 3. Procesar cada item
        var seenNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < bulk.Units.Count; i++)
        {
            var item = bulk.Units[i];
            var number = item.UnitNumber?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(number))
            {
                results.Add(new BulkItemResult<UnitResponseDto>(i, false, "Unit number is required", null));
                continue;
            }

            if (seenNumbers.Contains(number))
            {
                results.Add(new BulkItemResult<UnitResponseDto>(i, false, $"Duplicate UnitNumber '{number}' in request", null));
                continue;
            }
            seenNumbers.Add(number);

            if (existingNumbers.Contains(number))
            {
                results.Add(new BulkItemResult<UnitResponseDto>(i, false, $"Unit number '{number}' already exists in this community", null));
                continue;
            }

            if (item.Components == null || item.Components.Count == 0)
            {
                results.Add(new BulkItemResult<UnitResponseDto>(i, false, "At least one component is required", null));
                continue;
            }

            // Validar componentes internos
            var componentsResult = ValidateComponents(item.Components);
            if (!componentsResult.Success)
            {
                results.Add(new BulkItemResult<UnitResponseDto>(i, false, componentsResult.Error, null));
                continue;
            }

            // Exito - Preparar entidad
            var totalCoef = item.Components.Sum(x => x.CoefficientPct);
            var unit = new Unit
            {
                CommunityId = communityId,
                Number = number,
                CoefficientPct = totalCoef, // Compatibilidad
                CreatedAtUtc = DateTime.UtcNow
            };

            foreach (var c in item.Components)
            {
                unit.Components.Add(new UnitComponent
                {
                    CommunityId = communityId,
                    UnitId = unit.Id,
                    Type = c.Type.Trim(),
                    Code = c.Code.Trim(),
                    CoefficientPct = c.CoefficientPct,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            unitsToCreate.Add(unit);
            results.Add(new BulkItemResult<UnitResponseDto>(i, true, null, MapToResponse(unit)));
        }

        if (unitsToCreate.Count > 0)
        {
            await _repo.AddRangeAsync(unitsToCreate, ct);
        }

        return new BulkResponse<UnitResponseDto>(communityId, bulk.Units.Count, unitsToCreate.Count, bulk.Units.Count - unitsToCreate.Count, results);
    }

    private UnitResponseDto MapToResponse(Unit unit)
    {
        return new UnitResponseDto(
            unit.Id,
            unit.Number,
            unit.CoefficientPct,
            unit.Components.Select(c => new UnitComponentResponseDto(c.Type, c.Code, c.CoefficientPct)).ToList()
        );
    }

    private (bool Success, string? Error) ValidateComponents(List<CreateUnitComponentDto> components)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in components)
        {
            if (string.IsNullOrWhiteSpace(c.Type) || string.IsNullOrWhiteSpace(c.Code))
                return (false, "Component Type and Code are required");

            if (c.CoefficientPct <= 0)
                return (false, $"Component '{c.Type} {c.Code}' must have a CoefficientPct > 0");

            var key = $"{c.Type.Trim()}|{c.Code.Trim()}";
            if (seen.Contains(key))
                return (false, $"Duplicate component '{c.Type} {c.Code}' in the same unit");

            seen.Add(key);
        }
        return (true, null);
    }
}
