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
    int? MaxBookingsPerMonthPerUnit);

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
    int? MaxBookingsPerMonthPerUnit);

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
    int? MaxBookingsPerMonthPerUnit);
