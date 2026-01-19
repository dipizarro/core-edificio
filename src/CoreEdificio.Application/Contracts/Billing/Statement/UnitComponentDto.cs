namespace CoreEdificio.Application.Contracts.Billing.Statement;

public record UnitComponentDto(
    string Type,
    string Code,
    decimal CoefficientPct,
    bool IsActive
);
