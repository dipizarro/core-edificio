using CoreEdificio.Domain.Entities.Payments;

namespace CoreEdificio.Application.Contracts.Payments;

public record RegisterPaymentCommand(
    Guid UnitId,
    string Period,
    decimal Amount,
    PaymentMethod Method,
    DateTime? PaidAtUtc,
    string? Reference
);
