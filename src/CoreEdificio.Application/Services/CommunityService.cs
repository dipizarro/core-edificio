using CoreEdificio.Application.Common;
using CoreEdificio.Application.Contracts;
using CoreEdificio.Application.Contracts.Residents;
using CoreEdificio.Application.Interfaces;
using CoreEdificio.Application.Interfaces.Identity;
using CoreEdificio.Domain.Entities;

namespace CoreEdificio.Application.Services;

/// <summary>
/// Implementación de los casos de uso para la gestión de comunidades.
/// NOTA DE ARQUITECTURA: Idealmente debería implementar una interfaz ICommunityService 
/// para desacoplar completamente su inyección en la capa API.
/// </summary>
public class CommunityService
{
    private readonly ICommunityRepository _repo;
    private readonly IIdentityService _identity;

    public CommunityService(ICommunityRepository repo, IIdentityService identity)
    {
        _repo = repo;
        _identity = identity;
    }

    /// <summary>
    /// Crea una nueva comunidad validando precondiciones requeridas.
    /// </summary>
    public async Task<Community> CreateAsync(CreateCommunityCommand cmd, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.Name)) 
            throw new ValidationException("El nombre de la comunidad es requerido.");
            
        if (string.IsNullOrWhiteSpace(cmd.Address)) 
            throw new ValidationException("La dirección de la comunidad es requerida.");

        var community = new Community
        {
            Name = cmd.Name.Trim(),
            Address = cmd.Address.Trim()
        };

        await _repo.AddAsync(community, ct);
        return community;
    }

    /// <summary>
    /// Obtiene los residentes asociados a una comunidad con sus respectivas unidades.
    /// </summary>
    public async Task<List<ResidentWithUnitsDto>> GetResidentsWithUnitsAsync(Guid communityId, CancellationToken ct = default)
    {
        return await _identity.GetResidentsWithUnitsAsync(communityId, ct);
    }

    /// <summary>
    /// Obtiene una comunidad específica por su Id.
    /// </summary>
    public Task<Community?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return _repo.GetByIdAsync(id, ct);
    }

    /// <summary>
    /// Lista todas las comunidades del sistema.
    /// </summary>
    public Task<List<Community>> ListAsync(CancellationToken ct = default)
    {
        return _repo.ListAsync(ct);
    }
}
