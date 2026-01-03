using CoreEdificio.Application.Common;
using CoreEdificio.Application.Contracts.Payments;
using CoreEdificio.Application.Interfaces.Payments;
using CoreEdificio.Domain.Entities.Payments;

namespace CoreEdificio.Application.Services;

public class PaymentsService
{
    private readonly IPaymentRepository _payments;
    private readonly IBillingReadRepository _billing;

    public PaymentsService(IPaymentRepository payments, IBillingReadRepository billing)
    {
        _payments = payments;
        _billing = billing;
    }

    public async Task<PaymentDto> RegisterAsync(Guid communityId, RegisterPaymentCommand cmd, CancellationToken ct = default)
    {
        var period = NormalizePeriod(cmd.Period);

        if (cmd.UnitId == Guid.Empty) throw new ValidationException("UnitId is required.");
        if (cmd.Amount <= 0) throw new ValidationException("Amount must be > 0.");

        var issued = await _billing.IsPeriodIssuedAsync(communityId, period, ct);
        if (!issued) throw new ConflictException("Period is not issued. You can't register payments.");

        var chargeAmount = await _billing.GetUnitChargeAmountAsync(communityId, cmd.UnitId, period, ct);
        if (chargeAmount is null) throw new NotFoundException("Unit charge not found for this period.");

        var paidTotal = await _payments.GetPaidTotalAsync(communityId, cmd.UnitId, period, ct);
        var outstanding = decimal.Round(chargeAmount.Value - paidTotal, 2, MidpointRounding.AwayFromZero);

        var amount = decimal.Round(cmd.Amount, 2, MidpointRounding.AwayFromZero);
        if (amount > outstanding) throw new ConflictException($"Overpayment not allowed. Outstanding: {outstanding:0.00}");

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

    public async Task<UnitBalanceDto> GetBalanceAsync(Guid communityId, Guid unitId, string period, CancellationToken ct = default)
    {
        period = NormalizePeriod(period);

        var issued = await _billing.IsPeriodIssuedAsync(communityId, period, ct);
        if (!issued) throw new ConflictException("Period is not issued.");

        var chargeAmount = await _billing.GetUnitChargeAmountAsync(communityId, unitId, period, ct);
        if (chargeAmount is null) throw new NotFoundException("Unit charge not found for this period.");

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
        if (string.IsNullOrWhiteSpace(period)) throw new ValidationException("Period is required (YYYY-MM).");
        period = period.Trim();
        if (period.Length != 7 || period[4] != '-') throw new ValidationException("Period must be in format YYYY-MM.");
        return period;
    }

    public async Task<List<ArrearsUnitDto>> GetArrearsAsync(Guid communityId, string period, CancellationToken ct = default)
    {
        period = NormalizePeriod(period);

        var issued = await _billing.IsPeriodIssuedAsync(communityId, period, ct);
        if (!issued) throw new ConflictException("Period is not issued.");

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
