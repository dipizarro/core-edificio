namespace CoreEdificio.Api.Contracts;

public record CreateUnitRequest(string Number, decimal CoefficientPct, string? OwnerName, string? OwnerEmail);
