namespace CoreEdificio.Application.Contracts.Billing.Statement;

public record UnitStatementDto(
    Guid CommunityId,
    Guid UnitId,
    string UnitNumber,
    string Period,
    decimal PreviousBalance,
    decimal CurrentChargesTotal,
    decimal PaymentsTotal,
    decimal TotalDue,
    DateTime DueDate,
    List<StatementLineDto> Lines
);
