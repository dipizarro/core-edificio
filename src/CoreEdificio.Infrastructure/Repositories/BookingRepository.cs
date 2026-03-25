using CoreEdificio.Application.Interfaces;
using CoreEdificio.Domain.Entities;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Repositories;

/// <summary>
/// Repositorio encargado de gestionar el estado y persistencia de las Reservas (Bookings) de espacios comunes.
/// Implementa reglas de lectura rápida para evitar solapamientos.
/// </summary>
public class BookingRepository : IBookingRepository
{
    private readonly AppDbContext _db;

    public BookingRepository(AppDbContext db) => _db = db;

    /// <summary>
    /// Obtiene información de la Instalación requerida, sin seguimiento (NoTracking) para optimizar memoria en validaciones.
    /// </summary>
    public Task<Facility?> GetFacilityAsync(Guid communityId, Guid facilityId, CancellationToken ct = default)
        => _db.Facilities
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == facilityId && f.CommunityId == communityId, ct);

    /// <summary>
    /// Verifica rápidamente la existencia de una unidad particular dentro de su comunidad.
    /// </summary>
    public Task<bool> UnitExistsAsync(Guid communityId, Guid unitId, CancellationToken ct = default)
        => _db.Units.AnyAsync(u => u.Id == unitId && u.CommunityId == communityId, ct);

    /// <summary>
    /// Verifica lógicamente si el rango horario suministrado solapa con alguna reserva Aprobada o Pendiente.
    /// </summary>
    public Task<bool> HasOverlapAsync(Guid facilityId, DateTime startUtc, DateTime endUtc, CancellationToken ct = default)
        => _db.Bookings.AnyAsync(b =>
            b.FacilityId == facilityId &&
            (b.Status == BookingStatus.PendingApproval || b.Status == BookingStatus.Approved) &&
            b.StartAtUtc < endUtc &&
            b.EndAtUtc > startUtc,
            ct);

    /// <summary>
    /// Inserta una nueva reserva en base de datos.
    /// </summary>
    public async Task AddAsync(Booking booking, CancellationToken ct = default)
    {
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Recupera una reserva por su identificador primario (ID) manteniendo el seguimiento o tracking para posibles modificaciones (Aprobación/Rechazo).
    /// </summary>
    public Task<Booking?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Bookings.FirstOrDefaultAsync(x => x.Id == id, ct);

    /// <summary>
    /// Transacciona una actualización de estado sobre una reserva existente (e.g. Cambio a Cancelado, Aprobado).
    /// </summary>
    public async Task UpdateAsync(Booking booking, CancellationToken ct = default)
    {
        _db.Bookings.Update(booking);
        await _db.SaveChangesAsync(ct);
    }
}
