using CoreEdificio.Application.Contracts.Auth;
using CoreEdificio.Infrastructure.Auth;
using CoreEdificio.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CoreEdificio.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser> _users;
    private readonly JwtTokenService _jwt;

    public AuthController(
        SignInManager<ApplicationUser> signIn,
        UserManager<ApplicationUser> users,
        JwtTokenService jwt)
    {
        _signIn = signIn;
        _users = users;
        _jwt = jwt;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest("Email and password are required.");

        var user = await _users.FindByEmailAsync(req.Email.Trim());
        if (user is null) return Unauthorized("Invalid credentials.");

        var result = await _signIn.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (!result.Succeeded) return Unauthorized("Invalid credentials.");

        var (token, expiresAtUtc, roles) = await _jwt.CreateTokenAsync(user);

        var resp = new LoginResponse(
            Token: token,
            ExpiresAtUtc: expiresAtUtc,
            UserId: user.Id,
            Email: user.Email ?? req.Email.Trim(),
            CommunityId: user.CommunityId,
            UnitId: user.UnitId,
            Roles: roles
        );

        return Ok(resp);
    }
}
