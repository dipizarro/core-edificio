using CoreEdificio.Application.Contracts.Billing;

namespace CoreEdificio.Application.Contracts.Billing.Bulk;

public record CreateExpensesBulkCommand(List<CreateExpenseCommand> Expenses);
