namespace CoreEdificio.Application.Contracts.Bulk;

public record UnitComponentResponseDto(string Type, string Code, decimal CoefficientPct);

public record UnitResponseDto(Guid Id, string Number, decimal TotalCoefficientPct, List<UnitComponentResponseDto> Components);
