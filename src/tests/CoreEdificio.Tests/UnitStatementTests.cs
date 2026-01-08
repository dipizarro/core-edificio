using CoreEdificio.Application.Contracts.Billing;
using CoreEdificio.Application.Interfaces;
using CoreEdificio.Application.Interfaces.Billing;
using CoreEdificio.Application.Interfaces.Payments;
using CoreEdificio.Application.Services;
using CoreEdificio.Domain.Entities.Billing;
using CoreEdificio.Domain.Entities.Payments;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace CoreEdificio.Tests;

public class UnitStatementTests
{
    private readonly Mock<IExpenseRepository> _expenses = new();
    private readonly Mock<IBillingPeriodRepository> _periods = new();
    private readonly Mock<IUnitChargeRepository> _charges = new();
    private readonly Mock<IUnitReadRepository> _units = new();
    private readonly Mock<IPaymentRepository> _payments = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IConfiguration> _config = new();

    private readonly BillingService _service;

    public UnitStatementTests()
    {
        _service = new BillingService(
            _expenses.Object, 
            _periods.Object, 
            _charges.Object, 
            _units.Object, 
            _payments.Object, 
            _uow.Object, 
            _config.Object
        );

        // Setup Config defaults
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(x => x.Value).Returns("30"); // Default due day
        _config.Setup(x => x.GetSection(It.IsAny<string>())).Returns(mockSection.Object);
    }

    [Fact]
    public async Task GetUnitStatement_PreviousBalance_ShouldBeTotalCharges_WhenNoPayments()
    {
        // Arrange
        var communityId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var period = "2024-02";

        // Setup Unit
        _units.Setup(x => x.ListSnapshotsByCommunityAsync(communityId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(new List<UnitSnapshot> { new UnitSnapshot(unitId, "101", 10m) });

        // Setup Charges Before: 1000
        _charges.Setup(x => x.GetChargesBeforePeriodAsync(communityId, unitId, period, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<UnitCharge> { new UnitCharge { Amount = 1000m } });

        // Setup Payments Before: 0
        _payments.Setup(x => x.GetPaymentsBeforePeriodAsync(communityId, unitId, period, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Payment>());

        // Setup Current Period: Empty (to isolate previous balance check)
        _charges.Setup(x => x.GetChargesForPeriodAsync(communityId, unitId, period, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<UnitCharge>());
         _payments.Setup(x => x.GetPaymentsForPeriodAsync(communityId, unitId, period, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Payment>());

        // Act
        var result = await _service.GetUnitStatementAsync(communityId, unitId, period, CancellationToken.None);

        // Assert
        Assert.Equal(1000m, result.PreviousBalance);
        Assert.Equal(1000m, result.TotalDue); // No current charges/payments
    }

    [Fact]
    public async Task GetUnitStatement_PreviousBalance_ShouldBeChargesMinusPayments()
    {
        // Arrange
        var communityId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var period = "2024-02";

        _units.Setup(x => x.ListSnapshotsByCommunityAsync(communityId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(new List<UnitSnapshot> { new UnitSnapshot(unitId, "101", 10m) });

        // Charges: 1000
        _charges.Setup(x => x.GetChargesBeforePeriodAsync(communityId, unitId, period, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<UnitCharge> { new UnitCharge { Amount = 1000m } });

        // Payments: 400
        _payments.Setup(x => x.GetPaymentsBeforePeriodAsync(communityId, unitId, period, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Payment> { new Payment { Amount = 400m } });

        // Current Period Empty
        _charges.Setup(x => x.GetChargesForPeriodAsync(communityId, unitId, period, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<UnitCharge>());
        _payments.Setup(x => x.GetPaymentsForPeriodAsync(communityId, unitId, period, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Payment>());

        // Act
        var result = await _service.GetUnitStatementAsync(communityId, unitId, period, CancellationToken.None);

        // Assert
        Assert.Equal(600m, result.PreviousBalance); // 1000 - 400
    }

    [Fact]
    public async Task GetUnitStatement_TotalDue_ShouldSubtractCurrentPayments()
    {
        // Arrange
        var communityId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var period = "2024-02";

        _units.Setup(x => x.ListSnapshotsByCommunityAsync(communityId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(new List<UnitSnapshot> { new UnitSnapshot(unitId, "101", 10m) });

        // Previous Balance: 0
        _charges.Setup(x => x.GetChargesBeforePeriodAsync(communityId, unitId, period, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<UnitCharge>());
        _payments.Setup(x => x.GetPaymentsBeforePeriodAsync(communityId, unitId, period, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Payment>());

        // Current Charges: 500
        var issuedAt = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        _charges.Setup(x => x.GetChargesForPeriodAsync(communityId, unitId, period, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<UnitCharge> { 
                    new UnitCharge { Amount = 500m, BillingPeriod = new BillingPeriod { IssuedAtUtc = issuedAt } } 
                });

        // Current Payments: 200
        _payments.Setup(x => x.GetPaymentsForPeriodAsync(communityId, unitId, period, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Payment> { new Payment { Amount = 200m, PaidAtUtc = DateTime.UtcNow } });

        // Act
        var result = await _service.GetUnitStatementAsync(communityId, unitId, period, CancellationToken.None);

        // Assert
        Assert.Equal(0m, result.PreviousBalance);
        Assert.Equal(500m, result.CurrentChargesTotal);
        Assert.Equal(200m, result.PaymentsTotal);
        Assert.Equal(300m, result.TotalDue); // 0 + 500 - 200
    }
}
