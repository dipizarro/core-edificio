namespace CoreEdificio.Api.Contracts;

public record CreateBookingRequest(
    Guid UnitId,
    DateTime StartAt,
    DateTime EndAt,
    string? Notes);

public record BookingDto(
    Guid Id,
    Guid FacilityId,
    Guid UnitId,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string Status,
    string? Notes);

public record RejectBookingRequest(string Reason);
public record CancelBookingRequest(string? Reason);
