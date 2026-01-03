namespace CoreEdificio.Application.Contracts.Payments;

public record ArrearsUnitDto(
    Guid UnitId,
    string UnitNumber,
    decimal ChargeAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    BalanceStatus Status
);
