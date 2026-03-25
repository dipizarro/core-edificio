using CoreEdificio.Application.Interfaces.Payments;
using CoreEdificio.Domain.Entities.Payments;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Repositories.Payments;

/// <summary>
/// Repositorio de acceso a datos para Pagos (Abonos) gestionados en el sistema.
/// </summary>
public class PaymentRepository : IPaymentRepository
{
    private readonly AppDbContext _db;

    public PaymentRepository(AppDbContext db) => _db = db;

    /// <summary>
    /// Inserta de manera asíncrona un nuevo registro de pago asociado a una unidad en particular.
    /// </summary>
    public async Task AddAsync(Payment payment, CancellationToken ct = default)
    {
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Obtiene la sumatoria total de abonos registrados para una unidad en un periodo específico.
    /// Útil para calcular la deuda restante.
    /// </summary>
    public Task<decimal> GetPaidTotalAsync(Guid communityId, Guid unitId, string period, CancellationToken ct = default)
        => _db.Payments.AsNoTracking()
            .Where(x => x.CommunityId == communityId && x.UnitId == unitId && x.Period == period)
            .SumAsync(x => x.Amount, ct);

    /// <summary>
    /// Devuelve el historial de pagos asociados a la comunidad, permitiendo filtrar por Unidad y Periodo.
    /// </summary>
    public async Task<List<Payment>> ListAsync(Guid communityId, Guid? unitId, string? period, CancellationToken ct = default)
    {
        var q = _db.Payments.AsNoTracking().Where(x => x.CommunityId == communityId);

        if (unitId.HasValue) q = q.Where(x => x.UnitId == unitId.Value);
        if (!string.IsNullOrWhiteSpace(period)) q = q.Where(x => x.Period == period);

        return await q.OrderByDescending(x => x.PaidAtUtc).ToListAsync(ct);
    }

    /// <summary>
    /// Busca todos los pagos/abonos realizados en periodos cronológicamente anteriores al suministrado.
    /// Ayuda en la consolidación de Saldos Previos (Previous Balances).
    /// </summary>
    public Task<List<Payment>> GetPaymentsBeforePeriodAsync(Guid communityId, Guid unitId, string period, CancellationToken ct = default)
        => _db.Payments.AsNoTracking()
            .Where(x => x.CommunityId == communityId && x.UnitId == unitId && string.Compare(x.Period, period) < 0)
            .ToListAsync(ct);

    /// <summary>
    /// Retorna un detalle transaccional individualizado de todos los pagos que caen exclusivamente en el periodo actual.
    /// </summary>
    public Task<List<Payment>> GetPaymentsForPeriodAsync(Guid communityId, Guid unitId, string period, CancellationToken ct = default)
        => _db.Payments.AsNoTracking()
            .Where(x => x.CommunityId == communityId && x.UnitId == unitId && x.Period == period)
            .ToListAsync(ct);
}
