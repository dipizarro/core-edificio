namespace CoreEdificio.Api.Contracts;

public record CreateFacilityRequest(
    string Name,
    string? Description,
    int? Capacity,
    string ChargingMode,
    int RentAmountClp,
    int DepositAmountClp,
    bool RequiresApproval,
    int SlotDurationMinutes,
    int? MaxHoursPerBooking,
    int? MaxBookingsPerMonthPerUnit,
    int CancelPenaltyHours = 0,
    int LateCancelFineAmountClp = 0,
    int NoShowFineAmountClp = 0);

public record UpdateFacilityRequest(
    string Name,
    string? Description,
    int? Capacity,
    string ChargingMode,
    int RentAmountClp,
    int DepositAmountClp,
    bool RequiresApproval,
    int SlotDurationMinutes,
    int? MaxHoursPerBooking,
    int? MaxBookingsPerMonthPerUnit,
    int CancelPenaltyHours = 0,
    int LateCancelFineAmountClp = 0,
    int NoShowFineAmountClp = 0);

public record FacilityDto(
    Guid Id,
    Guid CommunityId,
    string Name,
    string? Description,
    bool IsActive,
    int? Capacity,
    string ChargingMode,
    int RentAmountClp,
    int DepositAmountClp,
    bool RequiresApproval,
    int SlotDurationMinutes,
    int? MaxHoursPerBooking,
    int? MaxBookingsPerMonthPerUnit,
    int CancelPenaltyHours,
    int LateCancelFineAmountClp,
    int NoShowFineAmountClp);
