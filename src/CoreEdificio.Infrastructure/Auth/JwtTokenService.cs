using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CoreEdificio.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CoreEdificio.Infrastructure.Auth;

public class JwtTokenService
{
    private readonly IConfiguration _config;
    private readonly UserManager<ApplicationUser> _userManager;

    public JwtTokenService(IConfiguration config, UserManager<ApplicationUser> userManager)
    {
        _config = config;
        _userManager = userManager;
    }

    public async Task<(string token, DateTime expiresAtUtc, List<string> roles)> CreateTokenAsync(ApplicationUser user)
    {
        var issuer = _config["Jwt:Issuer"]!;
        var audience = _config["Jwt:Audience"]!;
        var key = _config["Jwt:Key"]!;
        var expiresMinutes = int.Parse(_config["Jwt:ExpiresMinutes"] ?? "480");

        var roles = (await _userManager.GetRolesAsync(user)).ToList();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Email ?? ""),
        };

        // Roles
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        // Scope por comunidad/unidad (MVP)
        if (user.CommunityId is not null)
            claims.Add(new Claim("communityId", user.CommunityId.Value.ToString()));

        if (user.UnitId is not null)
            claims.Add(new Claim("unitId", user.UnitId.Value.ToString()));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(expiresMinutes);

        var jwt = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: creds
        );

        var token = new JwtSecurityTokenHandler().WriteToken(jwt);
        return (token, expiresAtUtc, roles);
    }
}
