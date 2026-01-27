namespace CoreEdificio.Api.Contracts;

public record BookingChargeDto(
    Guid ChargeId,
    string Kind, // Rent | Deposit | Fine
    string Description,
    int AmountClp,
    string Period,
    bool IsPaid);

public record BookingDetailDto(
    Guid Id,
    Guid CommunityId,
    Guid FacilityId,
    string FacilityName,
    Guid UnitId,
    string UnitNumber,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string Status,
    bool RequiresApproval,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? ApprovedAtUtc,
    string? RejectReason,
    DateTime? CancelledAtUtc,
    string? CancelReason,
    DateTime? CompletedAtUtc,
    List<BookingChargeDto> Charges);

public record BookingListDto(
    Guid Id,
    Guid FacilityId,
    string FacilityName,
    Guid UnitId,
    string UnitNumber,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string Status);
