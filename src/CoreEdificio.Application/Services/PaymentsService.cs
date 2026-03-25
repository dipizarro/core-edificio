using CoreEdificio.Application.Common;
using CoreEdificio.Application.Contracts.Payments;
using CoreEdificio.Application.Interfaces.Payments;
using CoreEdificio.Domain.Entities.Payments;

namespace CoreEdificio.Application.Services;

/// <summary>
/// Servicio responsable del procesamiento de abonos y emisión de deudas (Arrears) para los Residentes.
/// </summary>
public class PaymentsService
{
    private readonly IPaymentRepository _payments;
    private readonly IBillingReadRepository _billing;

    public PaymentsService(IPaymentRepository payments, IBillingReadRepository billing)
    {
        _payments = payments;
        _billing = billing;
    }

    /// <summary>
    /// Registra el abono (transferencia, depósito, etc.) comprobando que no supere el saldo pendiente.
    /// </summary>
    public async Task<PaymentDto> RegisterAsync(Guid communityId, RegisterPaymentCommand cmd, CancellationToken ct = default)
    {
        var period = NormalizePeriod(cmd.Period);

        if (cmd.UnitId == Guid.Empty) throw new ValidationException("El UnitId es obligatorio.");
        if (cmd.Amount <= 0) throw new ValidationException("El monto debe ser numérico mayor a cero.");

        var issued = await _billing.IsPeriodIssuedAsync(communityId, period, ct);
        if (!issued) throw new ConflictException("El periodo aún no ha sido emitido. No puede registrar pagos anticipados.");

        var chargeAmount = await _billing.GetUnitChargeAmountAsync(communityId, cmd.UnitId, period, ct);
        if (chargeAmount is null) throw new NotFoundException("No se encontró ningún cargo en el periodo para esta unidad.");

        var paidTotal = await _payments.GetPaidTotalAsync(communityId, cmd.UnitId, period, ct);
        var outstanding = decimal.Round(chargeAmount.Value - paidTotal, 2, MidpointRounding.AwayFromZero);

        var amount = decimal.Round(cmd.Amount, 2, MidpointRounding.AwayFromZero);
        if (amount > outstanding) throw new ConflictException($"No se permite el sobrepago. Deuda actual: {outstanding:0.00}");

        var payment = new Payment
        {
            CommunityId = communityId,
            UnitId = cmd.UnitId,
            Period = period,
            Amount = amount,
            Method = cmd.Method,
            PaidAtUtc = cmd.PaidAtUtc ?? DateTime.UtcNow,
            Reference = string.IsNullOrWhiteSpace(cmd.Reference) ? null : cmd.Reference.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        await _payments.AddAsync(payment, ct);

        return new PaymentDto(
            payment.Id,
            payment.CommunityId,
            payment.UnitId,
            payment.Period,
            payment.Amount,
            payment.Method,
            payment.PaidAtUtc,
            payment.Reference
        );
    }

    /// <summary>
    /// Consulta el saldo general para una sola Unidad. Identificando si está 'Pagado', 'Deuda' o 'Pago Parcial'.
    /// </summary>
    public async Task<UnitBalanceDto> GetBalanceAsync(Guid communityId, Guid unitId, string period, CancellationToken ct = default)
    {
        period = NormalizePeriod(period);

        var issued = await _billing.IsPeriodIssuedAsync(communityId, period, ct);
        if (!issued) throw new ConflictException("El periodo no se encuentra emitido para consultar su balance.");

        var chargeAmount = await _billing.GetUnitChargeAmountAsync(communityId, unitId, period, ct);
        if (chargeAmount is null) throw new NotFoundException("No hay cargo asignado para esta unidad.");

        var paidTotal = await _payments.GetPaidTotalAsync(communityId, unitId, period, ct);

        var outstanding = decimal.Round(chargeAmount.Value - paidTotal, 2, MidpointRounding.AwayFromZero);

        var status =
            paidTotal <= 0 ? BalanceStatus.Unpaid :
            outstanding <= 0 ? BalanceStatus.Paid :
            BalanceStatus.Partial;

        return new UnitBalanceDto(
            communityId,
            unitId,
            period,
            chargeAmount.Value,
            paidTotal,
            outstanding < 0 ? 0 : outstanding,
            status
        );
    }

    /// <summary>
    /// Lista la traza de auditoría de los últimos pagos realizados aplicando filtros opcionales de unidad o periodo.
    /// </summary>
    public async Task<List<PaymentDto>> ListAsync(Guid communityId, Guid? unitId, string? period, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(period))
            period = NormalizePeriod(period);

        var items = await _payments.ListAsync(communityId, unitId, period, ct);

        return items.Select(p => new PaymentDto(
            p.Id,
            p.CommunityId,
            p.UnitId,
            p.Period,
            p.Amount,
            p.Method,
            p.PaidAtUtc,
            p.Reference
        )).ToList();
    }

    private static string NormalizePeriod(string period)
    {
        if (string.IsNullOrWhiteSpace(period)) throw new ValidationException("El periodo es obligatorio (YYYY-MM).");
        period = period.Trim();
        if (period.Length != 7 || period[4] != '-') throw new ValidationException("El periodo debe mantener el formato de año y mes: YYYY-MM.");
        return period;
    }

    /// <summary>
    /// Genera la lista de unidades en estado de morosidad o pago parcial (Arrears) para un periodo.
    /// Útil para vistas del comité o administrador en el panel resumen.
    /// </summary>
    public async Task<List<ArrearsUnitDto>> GetArrearsAsync(Guid communityId, string period, CancellationToken ct = default)
    {
        period = NormalizePeriod(period);

        var issued = await _billing.IsPeriodIssuedAsync(communityId, period, ct);
        if (!issued) throw new ConflictException("El periodo no se encuentra emitido para buscar morosos.");

        var charges = await _billing.ListUnitChargesAsync(communityId, period, ct);
        if (charges.Count == 0) return [];

        // Pagos agrupados por unidad
        var payments = await _payments.ListAsync(communityId, null, period, ct);
        var paidByUnit = payments
            .GroupBy(p => p.UnitId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var result = new List<ArrearsUnitDto>();

        foreach (var ch in charges.OrderBy(x => x.UnitNumber))
        {
            var paid = paidByUnit.TryGetValue(ch.UnitId, out var p) ? p : 0m;
            var outstanding = decimal.Round(ch.ChargeAmount - paid, 2, MidpointRounding.AwayFromZero);

            if (outstanding <= 0) continue;

            var status = paid <= 0 ? BalanceStatus.Unpaid : BalanceStatus.Partial;

            result.Add(new ArrearsUnitDto(
                ch.UnitId,
                ch.UnitNumber,
                ch.ChargeAmount,
                paid,
                outstanding,
                status
            ));
        }

        return result;
    }
}
