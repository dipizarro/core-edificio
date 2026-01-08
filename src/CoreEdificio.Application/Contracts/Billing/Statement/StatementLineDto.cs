namespace CoreEdificio.Application.Contracts.Billing.Statement;

public record StatementLineDto(string Type, string Description, decimal Amount, DateTime Date, string Period);
