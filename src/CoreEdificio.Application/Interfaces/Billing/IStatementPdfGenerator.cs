using CoreEdificio.Application.Contracts.Billing.Statement;

namespace CoreEdificio.Application.Interfaces.Billing;

public interface IStatementPdfGenerator
{
    Task<byte[]> GenerateUnitStatementPdfAsync(UnitStatementDto statement, CancellationToken ct = default);
}
