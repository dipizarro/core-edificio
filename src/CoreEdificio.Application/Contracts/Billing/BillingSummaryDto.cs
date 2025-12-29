namespace CoreEdificio.Application.Contracts.Billing;

public record UnitChargeDto(Guid UnitId, string UnitNumber, decimal CoefficientPct, decimal Amount);

public record BillingSummaryDto(
    Guid CommunityId,
    string Period,
    decimal TotalExpenses,
    decimal TotalCoefficientPct,
    int UnitsCount,
    List<UnitChargeDto> Charges
);
