namespace CoreEdificio.Application.Contracts.Bulk;

public record CreateUnitComponentDto(string Type, string Code, decimal CoefficientPct);

public record CreateUnitWithComponentsDto(string UnitNumber, List<CreateUnitComponentDto> Components);

public record CreateUnitsWithComponentsBulkCommand(Guid CommunityId, List<CreateUnitWithComponentsDto> Units);
