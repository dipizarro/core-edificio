using CoreEdificio.Application.Common;
using CoreEdificio.Application.Contracts.Bulk;
using CoreEdificio.Application.Interfaces;
using CoreEdificio.Application.Services;
using CoreEdificio.Domain.Entities;
using Moq;
using Xunit;

namespace CoreEdificio.Tests.Bulk;

public class UnitBulkWithComponentsTests
{
    private readonly Mock<IUnitRepository> _repoMock;
    private readonly UnitService _service;

    public UnitBulkWithComponentsTests()
    {
        _repoMock = new Mock<IUnitRepository>();
        _service = new UnitService(_repoMock.Object);
    }

    [Fact]
    public async Task CreateBulkWithComponentsAsync_ShouldCreateUnits_WhenValid()
    {
        // Arrange
        var communityId = Guid.NewGuid();
        var units = new List<CreateUnitWithComponentsDto>
        {
            new("101", new List<CreateUnitComponentDto>
            {
                new("Dept", "101", 0.9m),
                new("Parking", "E1", 0.1m)
            }),
            new("102", new List<CreateUnitComponentDto>
            {
                new("Dept", "102", 1.0m)
            })
        };
        var command = new CreateUnitsWithComponentsBulkCommand(communityId, units);

        _repoMock.Setup(x => x.CommunityExistsAsync(communityId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repoMock.Setup(x => x.GetExistingUnitNumbersAsync(communityId, It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());

        // Act
        var result = await _service.CreateBulkWithComponentsAsync(communityId, command);

        // Assert
        Assert.Equal(2, result.Created);
        Assert.Equal(0, result.Failed);
        _repoMock.Verify(x => x.AddRangeAsync(It.Is<List<Unit>>(l => l.Count == 2), It.IsAny<CancellationToken>()), Times.Once);
        
        var u101 = result.Results[0].Data;
        Assert.NotNull(u101);
        Assert.Equal(1.0m, u101.TotalCoefficientPct);
        Assert.Equal(2, u101.Components.Count);
    }

    [Fact]
    public async Task CreateBulkWithComponentsAsync_ShouldHandleDuplicates_InRequest()
    {
        // Arrange
        var communityId = Guid.NewGuid();
        var units = new List<CreateUnitWithComponentsDto>
        {
            new("101", new List<CreateUnitComponentDto> { new("Dept", "101", 1.0m) }),
            new("101", new List<CreateUnitComponentDto> { new("Dept", "101", 1.0m) }) // Duplicate
        };
        var command = new CreateUnitsWithComponentsBulkCommand(communityId, units);

        _repoMock.Setup(x => x.CommunityExistsAsync(communityId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repoMock.Setup(x => x.GetExistingUnitNumbersAsync(communityId, It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());

        // Act
        var result = await _service.CreateBulkWithComponentsAsync(communityId, command);

        // Assert
        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.Failed);
        Assert.Contains("Duplicate", result.Results[1].Error);
    }

    [Fact]
    public async Task CreateBulkWithComponentsAsync_ShouldFailUnit_WhenComponentIsInvalid()
    {
        // Arrange
        var communityId = Guid.NewGuid();
        var units = new List<CreateUnitWithComponentsDto>
        {
            new("101", new List<CreateUnitComponentDto>
            {
                new("Dept", "101", -1.0m) // Invalid Coef
            })
        };
        var command = new CreateUnitsWithComponentsBulkCommand(communityId, units);

        _repoMock.Setup(x => x.CommunityExistsAsync(communityId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repoMock.Setup(x => x.GetExistingUnitNumbersAsync(communityId, It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());

        // Act
        var result = await _service.CreateBulkWithComponentsAsync(communityId, command);

        // Assert
        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Failed);
        Assert.Contains("must have a CoefficientPct > 0", result.Results[0].Error);
    }

    [Fact]
    public async Task CreateBulkWithComponentsAsync_ShouldHandlePartialSuccess_WhenUnitAlreadyInDB()
    {
        // Arrange
        var communityId = Guid.NewGuid();
        var units = new List<CreateUnitWithComponentsDto>
        {
            new("101", new List<CreateUnitComponentDto> { new("Dept", "101", 1.0m) }),
            new("102", new List<CreateUnitComponentDto> { new("Dept", "102", 1.0m) })
        };
        var command = new CreateUnitsWithComponentsBulkCommand(communityId, units);

        _repoMock.Setup(x => x.CommunityExistsAsync(communityId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repoMock.Setup(x => x.GetExistingUnitNumbersAsync(communityId, It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "101" });

        // Act
        var result = await _service.CreateBulkWithComponentsAsync(communityId, command);

        // Assert
        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.Failed);
        Assert.Contains("already exists", result.Results[0].Error);
        Assert.True(result.Results[1].Success);
    }
}
