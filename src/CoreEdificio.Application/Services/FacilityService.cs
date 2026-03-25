using CoreEdificio.Application.Common;
using CoreEdificio.Application.Interfaces;
using CoreEdificio.Domain.Entities;

namespace CoreEdificio.Application.Services;

public class CreateFacilityDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int? Capacity { get; set; }
    public string ChargingMode { get; set; } = null!;
    public int RentAmountClp { get; set; }
    public int DepositAmountClp { get; set; }
    public bool RequiresApproval { get; set; }
    public int SlotDurationMinutes { get; set; }
    public int? MaxHoursPerBooking { get; set; }
    public int? MaxBookingsPerMonthPerUnit { get; set; }
    public int CancelPenaltyHours { get; set; }
    public int LateCancelFineAmountClp { get; set; }
    public int NoShowFineAmountClp { get; set; }
}

public class UpdateFacilityDto : CreateFacilityDto { }

/// <summary>
/// Servicio de aplicación para la gestión de instalaciones (Facilities) de la comunidad.
/// </summary>
public class FacilityService
{
    private readonly IFacilityRepository _repo;
    private readonly ICommunityRepository _communityRepo;

    public FacilityService(IFacilityRepository repo, ICommunityRepository communityRepo)
    {
        _repo = repo;
        _communityRepo = communityRepo;
    }

    public Task<List<Facility>> ListAsync(Guid communityId, CancellationToken ct = default)
        => _repo.ListByCommunityAsync(communityId, ct);

    public Task<Facility?> GetByIdAsync(Guid communityId, Guid facilityId, CancellationToken ct = default)
        => _repo.GetByIdAsync(communityId, facilityId, ct);

    public async Task<Facility> CreateAsync(Guid communityId, CreateFacilityDto request, CancellationToken ct = default)
    {
        var community = await _communityRepo.GetByIdAsync(communityId, ct);
        if (community is null) throw new NotFoundException("Comunidad no encontrada.");

        if (!TryParseChargingMode(request.ChargingMode, out var chargingMode))
            throw new ValidationException("El ChargingMode debe ser Free, Paid, Deposit o PaidAndDeposit.");

        ValidateRules(request.Name, request.SlotDurationMinutes, chargingMode, request.RentAmountClp, request.DepositAmountClp);

        var facility = new Facility
        {
            CommunityId = communityId,
            Name = request.Name.Trim(),
            Description = request.Description,
            Capacity = request.Capacity,
            ChargingMode = chargingMode,
            RentAmountClp = request.RentAmountClp,
            DepositAmountClp = request.DepositAmountClp,
            RequiresApproval = request.RequiresApproval,
            SlotDurationMinutes = request.SlotDurationMinutes,
            MaxHoursPerBooking = request.MaxHoursPerBooking,
            MaxBookingsPerMonthPerUnit = request.MaxBookingsPerMonthPerUnit,
            CancelPenaltyHours = request.CancelPenaltyHours,
            LateCancelFineAmountClp = request.LateCancelFineAmountClp,
            NoShowFineAmountClp = request.NoShowFineAmountClp,
            CreatedAtUtc = DateTime.UtcNow,
            IsActive = true
        };

        await _repo.AddAsync(facility, ct);
        return facility;
    }

    public async Task<Facility> UpdateAsync(Guid communityId, Guid facilityId, UpdateFacilityDto request, CancellationToken ct = default)
    {
        var facility = await _repo.GetByIdAsync(communityId, facilityId, ct);
        if (facility is null) throw new NotFoundException("Instalación no encontrada.");

        if (!TryParseChargingMode(request.ChargingMode, out var chargingMode))
            throw new ValidationException("El ChargingMode debe ser Free, Paid, Deposit o PaidAndDeposit.");

        ValidateRules(request.Name, request.SlotDurationMinutes, chargingMode, request.RentAmountClp, request.DepositAmountClp);

        facility.Name = request.Name.Trim();
        facility.Description = request.Description;
        facility.Capacity = request.Capacity;
        facility.ChargingMode = chargingMode;
        facility.RentAmountClp = request.RentAmountClp;
        facility.DepositAmountClp = request.DepositAmountClp;
        facility.RequiresApproval = request.RequiresApproval;
        facility.SlotDurationMinutes = request.SlotDurationMinutes;
        facility.MaxHoursPerBooking = request.MaxHoursPerBooking;
        facility.MaxBookingsPerMonthPerUnit = request.MaxBookingsPerMonthPerUnit;
        facility.CancelPenaltyHours = request.CancelPenaltyHours;
        facility.LateCancelFineAmountClp = request.LateCancelFineAmountClp;
        facility.NoShowFineAmountClp = request.NoShowFineAmountClp;

        await _repo.UpdateAsync(facility, ct);
        return facility;
    }

    public async Task<Facility> DeactivateAsync(Guid communityId, Guid facilityId, CancellationToken ct = default)
    {
        var facility = await _repo.GetByIdAsync(communityId, facilityId, ct);
        if (facility is null) throw new NotFoundException("Instalación no encontrada.");

        if (!facility.IsActive) return facility;

        facility.IsActive = false;
        await _repo.UpdateAsync(facility, ct);

        return facility;
    }

    private static bool TryParseChargingMode(string chargingMode, out FacilityChargingMode mode)
    {
        mode = default;
        if (string.IsNullOrWhiteSpace(chargingMode)) return false;
        if (!Enum.TryParse(chargingMode, true, out FacilityChargingMode parsed)) return false;
        if (!Enum.IsDefined(parsed)) return false;
        
        mode = parsed;
        return true;
    }

    private static void ValidateRules(string name, int slotDurationMinutes, FacilityChargingMode chargingMode, int rentAmountClp, int depositAmountClp)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("El nombre es requerido.");

        if (slotDurationMinutes <= 0)
            throw new ValidationException("La duración del bloque debe ser mayor a cero minutos.");

        var requiresRent = chargingMode is FacilityChargingMode.Paid or FacilityChargingMode.PaidAndDeposit;
        var requiresDeposit = chargingMode is FacilityChargingMode.Deposit or FacilityChargingMode.PaidAndDeposit;

        if (chargingMode == FacilityChargingMode.Free)
        {
            if (rentAmountClp != 0 || depositAmountClp != 0)
                throw new ValidationException("Las instalaciones gratuitas deben tener montos de arriendo y garantía en 0.");
        }

        if (requiresRent && rentAmountClp <= 0)
            throw new ValidationException("El monto de arriendo debe ser mayor a cero.");

        if (requiresDeposit && depositAmountClp <= 0)
            throw new ValidationException("El monto de garantía debe ser mayor a cero.");
    }
}
