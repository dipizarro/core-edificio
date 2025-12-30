namespace CoreEdificio.Domain.Entities.Payments;

public enum PaymentMethod
{
    Cash = 0,
    Transfer = 1,
    DebitCard = 2,
    CreditCard = 3,
    Other = 99
}
