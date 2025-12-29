namespace CoreEdificio.Application.Contracts.Billing;

public record CreateExpenseCommand(string Period, string Description, decimal Amount);
