using CoreEdificio.Application.Contracts.Admin;
using CoreEdificio.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreEdificio.Api.Controllers;

[Authorize(Roles = "Committee,Admin")]
[ApiController]
[Route("api/admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly UserProvisioningService _service;

    public AdminUsersController(UserProvisioningService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest req)
    {
        var user = await _service.CreateUserAsync(
            req.Email,
            req.Password,
            req.Role,
            req.CommunityId,
            req.UnitId
        );

        return CreatedAtAction(nameof(Create), new
        {
            user.Id,
            user.Email,
            req.Role,
            req.CommunityId,
            req.UnitId
        });
    }
}
