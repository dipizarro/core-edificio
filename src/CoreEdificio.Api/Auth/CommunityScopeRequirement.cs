using Microsoft.AspNetCore.Authorization;

namespace CoreEdificio.Api.Auth;

public record CommunityScopeRequirement() : IAuthorizationRequirement;

public record UnitScopeRequirement() : IAuthorizationRequirement;
