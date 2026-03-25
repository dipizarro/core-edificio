using CoreEdificio.Api.Auth;
using CoreEdificio.Application.Contracts;
using CoreEdificio.Application.Contracts.Residents;
using CoreEdificio.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreEdificio.Api.Controllers;

[Authorize(Roles = "Committee,Admin")]
[Authorize(Policy = AuthPolicies.CommunityScope)]
[ApiController]
[Route("api/communities")]
public class CommunitiesController : ControllerBase
{
    private readonly CommunityService _service;
    public CommunitiesController(CommunityService service) => _service = service;

    /// <summary>
    /// Registra una nueva comunidad (edificio/condominio) dentro de la plataforma.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(CreateCommunityCommand cmd, CancellationToken ct)
    {
        var created = await _service.CreateAsync(cmd, ct);
        return CreatedAtAction(nameof(GetById), new { communityId = created.Id }, created);
    }

    /// <summary>
    /// Consulta la información general de una comunidad específica.
    /// </summary>
    [HttpGet("{communityId:guid}")]
    public async Task<IActionResult> GetById(Guid communityId, CancellationToken ct)
    {
        var c = await _service.GetByIdAsync(communityId, ct);
        return c is null ? NotFound() : Ok(c);
    }

    /// <summary>
    /// Lista el padrón de residentes asociados a la comunidad incluyendo sus unidades (departamentos).
    /// </summary>
    [HttpGet("{communityId:guid}/residents")]
    [ProducesResponseType(typeof(List<ResidentWithUnitsDto>), 200)]
    public async Task<IActionResult> GetResidents(Guid communityId, CancellationToken ct)
    {
        var residents = await _service.GetResidentsWithUnitsAsync(communityId, ct);
        return Ok(residents);
    }

    /// <summary>
    /// Obtiene el listado completo de comunidades disponibles (Uso administrativo).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _service.ListAsync(ct));
}
