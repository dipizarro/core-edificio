using CoreEdificio.Application.Contracts;

namespace CoreEdificio.Application.Contracts.Bulk;

public record CreateUnitsBulkCommand(List<CreateUnitCommand> Units);
