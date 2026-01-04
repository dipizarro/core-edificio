namespace CoreEdificio.Application.Contracts.Auth;

public record LoginResponse(
    string Token,
    DateTime ExpiresAtUtc,
    Guid UserId,
    string Email,
    Guid? CommunityId,
    Guid? UnitId,
    List<string> Roles
);
