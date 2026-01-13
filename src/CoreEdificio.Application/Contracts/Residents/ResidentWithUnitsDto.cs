namespace CoreEdificio.Application.Contracts.Residents;

public record ResidentWithUnitsDto(
    Guid UserId,
    string Email,
    string Role,
    List<UnitRefDto> Units
);
