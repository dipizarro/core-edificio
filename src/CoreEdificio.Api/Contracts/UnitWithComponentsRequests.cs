using System.ComponentModel.DataAnnotations;

namespace CoreEdificio.Api.Contracts;

public record CreateUnitComponentRequest(
    [Required] string Type,
    [Required] string Code,
    [Required] decimal CoefficientPct
);

public record CreateUnitWithComponentsRequest(
    [Required] string UnitNumber,
    [Required, MinLength(1)] List<CreateUnitComponentRequest> Components
);

public record CreateUnitsWithComponentsBulkRequest(
    [Required, MinLength(1)] List<CreateUnitWithComponentsRequest> Units
);
