using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace CoreEdificio.Api.Auth;

public class CommunityScopeHandler : AuthorizationHandler<CommunityScopeRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, CommunityScopeRequirement requirement)
    {
        // Admin => always allowed
        if (context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var http = GetHttpContext(context);
        if (http is null) return Task.CompletedTask;

        if (!TryGetRouteGuid(http, "communityId", out var routeCommunityId))
            return Task.CompletedTask;

        var claimVal = context.User.FindFirst(AuthClaims.CommunityId)?.Value;
        if (!Guid.TryParse(claimVal, out var tokenCommunityId))
            return Task.CompletedTask;

        if (tokenCommunityId == routeCommunityId)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }

    private static HttpContext? GetHttpContext(AuthorizationHandlerContext context)
    {
        return context.Resource switch
        {
            HttpContext http => http,
            Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext mvc => mvc.HttpContext,
            _ => null
        };
    }

    private static bool TryGetRouteGuid(HttpContext http, string key, out Guid value)
    {
        value = default;
        if (!http.Request.RouteValues.TryGetValue(key, out var raw)) return false;
        return Guid.TryParse(raw?.ToString(), out value);
    }
}

public class UnitScopeHandler : AuthorizationHandler<UnitScopeRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, UnitScopeRequirement requirement)
    {
        // Admin => always allowed
        if (context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var http = GetHttpContext(context);
        if (http is null) return Task.CompletedTask;

        if (!TryGetRouteGuid(http, "unitId", out var routeUnitId))
            return Task.CompletedTask;

        // Committee: can access any unit inside their community
        if (context.User.IsInRole("Committee"))
        {
            if (!TryGetRouteGuid(http, "communityId", out var routeCommunityId))
                return Task.CompletedTask;

            var claimCommunity = context.User.FindFirst(AuthClaims.CommunityId)?.Value;
            if (!Guid.TryParse(claimCommunity, out var tokenCommunityId))
                return Task.CompletedTask;

            if (tokenCommunityId == routeCommunityId)
                context.Succeed(requirement);

            return Task.CompletedTask;
        }

        // Resident: must match unitId claim exactly
        if (context.User.IsInRole("Resident"))
        {
            var claimUnit = context.User.FindFirst(AuthClaims.UnitId)?.Value;
            if (!Guid.TryParse(claimUnit, out var tokenUnitId))
                return Task.CompletedTask;

            if (tokenUnitId == routeUnitId)
                context.Succeed(requirement);

            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    private static HttpContext? GetHttpContext(AuthorizationHandlerContext context)
    {
        return context.Resource switch
        {
            HttpContext http => http,
            Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext mvc => mvc.HttpContext,
            _ => null
        };
    }

    private static bool TryGetRouteGuid(HttpContext http, string key, out Guid value)
    {
        value = default;
        if (!http.Request.RouteValues.TryGetValue(key, out var raw)) return false;
        return Guid.TryParse(raw?.ToString(), out value);
    }
}
