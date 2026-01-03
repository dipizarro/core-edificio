namespace CoreEdificio.Application.Contracts.Payments;

public enum BalanceStatus
{
    Unpaid = 0,
    Partial = 1,
    Paid = 2
}

public record UnitBalanceDto(
    Guid CommunityId,
    Guid UnitId,
    string Period,
    decimal ChargeAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    BalanceStatus Status
);
