using CoreEdificio.Application.Interfaces.Billing;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;
    public UnitOfWork(AppDbContext db) => _db = db;

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        // Si ya hay transacción, ejecuta directo
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
