using CoreEdificio.Application.Interfaces.Billing;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Persistence;

/// <summary>
/// Implementación del Patrón Unit of Work para asegurar transacciones atómicas
/// en la capa de persistencia mediante Entity Framework Core.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    public UnitOfWork(AppDbContext db) => _db = db;

    /// <summary>
    /// Ejecuta un bloque de operaciones dentro de un contexto transaccional.
    /// Garantiza propiedades ACID, revirtiendo (Rollback) en caso de excepciones.
    /// </summary>
    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        // Si ya hay transacción activa, ejecuta directo (Nested execution support)
        if (_db.Database.CurrentTransaction is not null)
        {
            await action(ct);
            return;
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await action(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
