namespace CoreEdificio.Application.Contracts.Admin;

public record CreateUserRequest(
    string Email,
    string Password,
    string Role,
    Guid? CommunityId,
    Guid? UnitId
);
