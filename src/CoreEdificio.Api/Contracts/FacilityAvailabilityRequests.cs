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
