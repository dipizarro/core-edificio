using CoreEdificio.Application.Common;
using CoreEdificio.Application.Contracts;
using CoreEdificio.Application.Contracts.Bulk;
using CoreEdificio.Application.Interfaces;
using CoreEdificio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Application.Services;

/// <summary>
/// Servicio de aplicación encargado de la gestión integral de Unidades (Departamentos, Estacionamientos, etc.)
/// y su carga masiva dentro de una comunidad.
/// </summary>
public class UnitService
{
    private readonly IUnitRepository _repo;

    public UnitService(IUnitRepository repo) => _repo = repo;

    /// <summary>
    /// Crea una nueva unidad de forma individual, validando que el número no esté duplicado.
    /// Crea automáticamente un componente por defecto ("Department") para representar la unidad base.
    /// </summary>
    public async Task<Unit> CreateAsync(Guid communityId, CreateUnitCommand cmd, CancellationToken ct = default)
    {
        if (!await _repo.CommunityExistsAsync(communityId, ct))
            throw new NotFoundException("Comunidad no encontrada.");

        if (string.IsNullOrWhiteSpace(cmd.UnitNumber))
            throw new ValidationException("El número de unidad es requerido.");

        if (cmd.CoefficientPct <= 0 || cmd.CoefficientPct > 100)
            throw new ValidationException("El porcentaje de coeficiente debe ser > 0 y <= 100.");

        var number = cmd.UnitNumber.Trim();

        if (await _repo.UnitNumberExistsAsync(communityId, number, ct))
            throw new ConflictException("El número de unidad ya existe en esta comunidad.");

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

    /// <summary>
    /// Lista todas las unidades pertenecientes a una comunidad.
    /// </summary>
    public Task<List<Unit>> ListByCommunityAsync(Guid communityId, CancellationToken ct = default)
        => _repo.ListByCommunityAsync(communityId, ct);

    /// <summary>
    /// Obtiene un resumen con la cantidad total de unidades y la suma de sus coeficientes (prorrateo).
    /// </summary>
    public Task<(int Count, decimal TotalCoefficientPct)> GetCoefficientSummaryAsync(Guid communityId, CancellationToken ct = default)
        => _repo.GetCoefficientSummaryAsync(communityId, ct);

    /// <summary>
    /// Procesa la creación masiva de unidades simples (sin detalle de componentes adicionales).
    /// Filtra duplicados y valida datos antes de persistir en masa.
    /// </summary>
    public async Task<BulkResponse<UnitResponseDto>> CreateBulkAsync(Guid communityId, CreateUnitsBulkCommand bulk, CancellationToken ct = default)
    {
        if (!await _repo.CommunityExistsAsync(communityId, ct))
            throw new NotFoundException("Comunidad no encontrada.");

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

        // Lógica Compleja: Iteramos sobre los elementos, validando individualmente si ya existen
        // en la base de datos o si vienen duplicados en la misma petición (seenNumbers).
        // Solo las unidades válidas se preparan para ser insertadas.
        foreach (var item in normalizedItems)
        {
            if (!item.IsValid)
            {
                results.Add(new BulkItemResult<UnitResponseDto>(item.Index, false, "Datos inválidos (Se requiere Número y Coeficiente > 0)", null));
                continue;
            }

            if (seenNumbers.Contains(item.Number))
            {
                results.Add(new BulkItemResult<UnitResponseDto>(item.Index, false, "Comando duplicado en la misma petición", null));
                continue;
            }
            seenNumbers.Add(item.Number);

            if (existingNumbers.Contains(item.Number))
            {
                results.Add(new BulkItemResult<UnitResponseDto>(item.Index, false, "El número de unidad ya existe", null));
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

            // Componente base por defecto
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

    /// <summary>
    /// Crea en masa múltiples unidades junto con sus componentes detallados (bodegas, estacionamientos).
    /// Asegura que no existan colisiones de componentes (códigos repetidos) en toda la comunidad.
    /// </summary>
    public async Task<BulkResponse<UnitResponseDto>> CreateBulkWithComponentsAsync(Guid communityId, CreateUnitsWithComponentsBulkCommand bulk, CancellationToken ct = default)
    {
        if (!await _repo.CommunityExistsAsync(communityId, ct))
            throw new NotFoundException("Comunidad no encontrada.");

        var results = new List<BulkItemResult<UnitResponseDto>>();
        var unitsToCreate = new List<Unit>();

        if (bulk.Units is null || bulk.Units.Count == 0)
        {
            return new BulkResponse<UnitResponseDto>(communityId, 0, 0, 0, results);
        }

        // Lógica Compleja: Detección global de duplicados
        // Prevenimos la colisión buscando tanto en los identificadores principales (UnitNumber)
        // como en la combinación de componentes (Type + Code). Se extraen las peticiones y se verifican
        // en bloque contra la base de datos para minimizar los queries (optimización de acceso a datos).
        
        // 1. Detección de duplicados de UnitNumber
        var requestNumbers = bulk.Units.Select(x => x.UnitNumber?.Trim() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingNumbers = await _repo.GetExistingUnitNumbersAsync(communityId, requestNumbers, ct);

        // 2. Detección de duplicados de COMPONENTES en el request (Global)
        var allRequestComponents = bulk.Units
            .SelectMany(u => u.Components ?? [])
            .GroupBy(c => $"{c.Type?.Trim()}|{c.Code?.Trim()}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var componentsSharedInRequest = allRequestComponents.Where(x => x.Value > 1).Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 3. Consultar componentes existentes en DB
        var existingComponentKeys = await _repo.GetExistingComponentKeysAsync(communityId, allRequestComponents.Keys, ct);

        // 4. Procesar cada item
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

            // Validar componentes internos y colisiones globales/DB
            var compValidation = ValidateUnitComponents(item.Components, componentsSharedInRequest, existingComponentKeys);
            if (!compValidation.Success)
            {
                results.Add(new BulkItemResult<UnitResponseDto>(i, false, compValidation.Error, null));
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
            try
            {
                await _repo.AddRangeAsync(unitsToCreate, ct);
            }
            catch (DbUpdateException)
            {
                // Estrategia de Fallback: Ocurre ante una recarga concurrente de la entidad, 
                // bloqueando el guardado. Se marca la transacción actual como fallida.
                return HandlePersistenceError(communityId, bulk, results);
            }
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

    private (bool Success, string? Error) ValidateUnitComponents(
        List<CreateUnitComponentDto> components, 
        HashSet<string> sharedInRequest, 
        HashSet<string> existingInDb)
    {
        var unitSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in components)
        {
            if (string.IsNullOrWhiteSpace(c.Type) || string.IsNullOrWhiteSpace(c.Code))
                return (false, "Component Type and Code are required");

            if (c.CoefficientPct <= 0)
                return (false, $"Component '{c.Type} {c.Code}' must have a CoefficientPct > 0");

            var key = $"{c.Type.Trim()}|{c.Code.Trim()}";
            
            if (unitSeen.Contains(key))
                return (false, $"Duplicate component '{c.Type} {c.Code}' in the same unit");
            unitSeen.Add(key);

            if (sharedInRequest.Contains(key))
                return (false, $"Duplicated component in request: {c.Type} {c.Code} is assigned to multiple units");

            if (existingInDb.Contains(key))
                return (false, $"Component already assigned in community: {c.Type} {c.Code}");
        }
        return (true, null);
    }

    private BulkResponse<UnitResponseDto> HandlePersistenceError(Guid communityId, CreateUnitsWithComponentsBulkCommand bulk, List<BulkItemResult<UnitResponseDto>> results)
    {
        foreach (var res in results.Where(x => x.Success).ToList())
        {
            results[res.Index] = new BulkItemResult<UnitResponseDto>(res.Index, false, "Violación de restricción de base de datos (concurrencia detectada en asignación de componentes)", null);
        }
        
        return new BulkResponse<UnitResponseDto>(communityId, bulk.Units.Count, 0, bulk.Units.Count, results);
    }
}
