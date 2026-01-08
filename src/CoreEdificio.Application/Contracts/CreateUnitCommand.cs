namespace CoreEdificio.Application.Contracts;

public record CreateUnitCommand(string UnitNumber, decimal CoefficientPct, string? OwnerName, string? OwnerEmail);
