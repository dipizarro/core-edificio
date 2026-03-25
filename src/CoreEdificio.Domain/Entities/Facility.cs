namespace CoreEdificio.Domain.Entities;

/// <summary>
/// Representa una instalación o espacio común (ej. Quincho, Sala de Eventos) dentro de una comunidad.
/// </summary>
public class Facility
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CommunityId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    
    /// <summary>
    /// Indica si la instalación está disponible para reservas.
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    public int? Capacity { get; set; }
    public FacilityChargingMode ChargingMode { get; set; }
    public int RentAmountClp { get; set; }
    public int DepositAmountClp { get; set; }
    
    /// <summary>
    /// Indica si la reserva requiere aprobación por parte de la administración.
    /// </summary>
    public bool RequiresApproval { get; set; }
    
    /// <summary>
    /// Duración en minutos de cada bloque reservable (ej. 60 minutos).
    /// </summary>
    public int SlotDurationMinutes { get; set; }
    public int? MaxHoursPerBooking { get; set; }
    public int? MaxBookingsPerMonthPerUnit { get; set; }
    
    public int CancelPenaltyHours { get; set; }
    public int LateCancelFineAmountClp { get; set; }
    public int NoShowFineAmountClp { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Valida las reglas de negocio base para la configuración de la instalación.
    /// </summary>
    /// <exception cref="InvalidOperationException">Se lanza si la configuración es inconsistente.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidOperationException("Facility name is required.");

        if (SlotDurationMinutes <= 0)
            throw new InvalidOperationException("Slot duration must be greater than zero.");

        var requiresRent = ChargingMode is FacilityChargingMode.Paid or FacilityChargingMode.PaidAndDeposit;
        var requiresDeposit = ChargingMode is FacilityChargingMode.Deposit or FacilityChargingMode.PaidAndDeposit;

        if (requiresRent && RentAmountClp <= 0)
            throw new InvalidOperationException("Rent amount must be greater than zero for paid facilities.");

        if (requiresDeposit && DepositAmountClp <= 0)
            throw new InvalidOperationException("Deposit amount must be greater than zero for deposit-based facilities.");

        if (ChargingMode == FacilityChargingMode.Free && (RentAmountClp != 0 || DepositAmountClp != 0))
        {
            throw new InvalidOperationException("Free facilities must have zero rent and deposit amounts.");
        }
    }
}
