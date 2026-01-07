using CoreEdificio.Application.Contracts;
using CoreEdificio.Application.Contracts.Bulk;
using CoreEdificio.Application.Contracts.Billing;
using CoreEdificio.Application.Contracts.Billing.Bulk;
using CoreEdificio.Application.Interfaces;
using CoreEdificio.Application.Interfaces.Billing;
using CoreEdificio.Application.Services;
using CoreEdificio.Domain.Entities;
using CoreEdificio.Domain.Entities.Billing;
using Xunit;
using Moq;

namespace CoreEdificio.Tests;

public class BulkOperationsTests
{
    // Mock setup
    private readonly Mock<IUnitRepository> _unitRepo = new();
    private readonly Mock<IExpenseRepository> _expenses = new();
    private readonly Mock<IBillingPeriodRepository> _periods = new();
    private readonly Mock<IUnitChargeRepository> _charges = new();
    private readonly Mock<IUnitReadRepository> _unitRead = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private readonly UnitService _unitService;
    private readonly BillingService _billingService;

    public BulkOperationsTests()
    {
        _unitService = new UnitService(_unitRepo.Object);
        _billingService = new BillingService(_expenses.Object, _periods.Object, _charges.Object, _unitRead.Object, _uow.Object);
    }

    [Fact]
    public async Task CreateBulk_ShouldDetectDuplicatesInRequest()
    {
        // Arrange
        var communityId = Guid.NewGuid();
        _unitRepo.Setup(x => x.CommunityExistsAsync(communityId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _unitRepo.Setup(x => x.GetExistingUnitNumbersAsync(communityId, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new HashSet<string>());

        var cmd = new CreateUnitsBulkCommand(new List<CreateUnitCommand>
        {
            new CreateUnitCommand("101", 1.0m, "Owner 1", null),
            new CreateUnitCommand("101", 2.0m, "Owner Dup", null), // Duplicado
            new CreateUnitCommand("102", 1.5m, "Owner 2", null)
        });

        // Act
        var result = await _unitService.CreateBulkAsync(communityId, cmd);

        // Assert
        Assert.Equal(3, result.Total);
        Assert.Equal(2, result.Created);
        Assert.Equal(1, result.Failed);

        var first = result.Results.First(x => x.Index == 0);
        Assert.True(first.Success);
        
        var duplicate = result.Results.First(x => x.Index == 1);
        Assert.False(duplicate.Success);
        Assert.Contains("Duplicate in request", duplicate.Error);

        var last = result.Results.First(x => x.Index == 2);
        Assert.True(last.Success);
    }

    [Fact]
    public async Task CreateExpensesBulk_ShouldValidateAmount()
    {
        // Arrange
        var communityId = Guid.NewGuid();
        var cmd = new CreateExpensesBulkCommand(new List<CreateExpenseCommand>
        {
            new CreateExpenseCommand("2024-01", "Valid Expense", 100m),
            new CreateExpenseCommand("2024-01", "Invalid Expense", 0m), // Invalid amount
            new CreateExpenseCommand("2024-01", "Valid 2", 50m)
        });

        // Act
        var result = await _billingService.CreateExpensesBulkAsync(communityId, cmd);

        // Assert
        Assert.Equal(3, result.Total);
        Assert.Equal(2, result.Created);
        Assert.Equal(1, result.Failed);

        var invalid = result.Results.First(x => x.Index == 1);
        Assert.False(invalid.Success);
        Assert.Contains("Amount must be > 0", invalid.Error);
    }
}
