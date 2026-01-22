namespace CoreEdificio.Api.Contracts;

public record FacilityAvailabilityResponse(
    Guid CommunityId,
    Guid FacilityId,
    DateTime FromUtc,
    DateTime ToUtc,
    List<AvailabilityIntervalDto> Intervals);

public record AvailabilityIntervalDto(
    string Kind, // "Booking" | "Block"
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string? StatusOrReason,
    Guid? BookingId,
    Guid? BlockId,
    string? UnitNumber,
    bool IsTentative);

public record FacilityAvailabilitySlotsResponse(
    Guid CommunityId,
    Guid FacilityId,
    DateTime FromUtc,
    DateTime ToUtc,
    int SlotMinutes,
    List<AvailabilitySlotDto> Slots);

public record AvailabilitySlotDto(
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string Status, // "Free" | "Pending" | "Booked" | "Blocked"
    string? ReasonOrStatus,
    Guid? BookingId,
    Guid? BlockId);
