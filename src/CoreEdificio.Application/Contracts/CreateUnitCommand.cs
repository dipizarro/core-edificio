namespace CoreEdificio.Application.Contracts;

public record CreateUnitCommand(string Number, decimal CoefficientPct, string? OwnerName, string? OwnerEmail);
