using System.Security.Claims;

namespace CoreEdificio.Api.Auth;

public static class UserContext
{
    public static Guid GetUserId(ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static Guid? GetCommunityId(ClaimsPrincipal user)
    {
        var val = user.FindFirst("communityId")?.Value;
        return val is null ? null : Guid.Parse(val);
    }

    public static Guid? GetUnitId(ClaimsPrincipal user)
    {
        var val = user.FindFirst("unitId")?.Value;
        return val is null ? null : Guid.Parse(val);
    }

    public static bool IsInRole(ClaimsPrincipal user, string role)
        => user.IsInRole(role);
}
