namespace CoreEdificio.Api.Contracts;

public record CreateFacilityBlockRequest(
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string Reason);

public record FacilityBlockDto(
    Guid Id,
    Guid FacilityId,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string Reason,
    bool IsActive,
    DateTime CreatedAtUtc);
