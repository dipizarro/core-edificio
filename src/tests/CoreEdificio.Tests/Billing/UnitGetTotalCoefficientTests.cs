using CoreEdificio.Domain.Entities;
using Xunit;

namespace CoreEdificio.Tests.Billing;

public class UnitGetTotalCoefficientTests
{
    [Fact]
    public void GetTotalCoefficientPct_WithActiveComponents_ShouldSumThem()
    {
        // Arrange
        var unit = new Unit { Number = "101", CoefficientPct = 1.0m };
        unit.Components.Add(new UnitComponent { Type = "Dept", CoefficientPct = 0.8m, IsActive = true });
        unit.Components.Add(new UnitComponent { Type = "Parking", CoefficientPct = 0.2m, IsActive = true });

        // Act
        var total = unit.GetTotalCoefficientPct();

        // Assert
        Assert.Equal(1.0m, total);
    }

    [Fact]
    public void GetTotalCoefficientPct_WithInactiveComponents_ShouldIgnoreThem()
    {
        // Arrange
        var unit = new Unit { Number = "101", CoefficientPct = 1.0m };
        unit.Components.Add(new UnitComponent { Type = "Dept", CoefficientPct = 0.8m, IsActive = true });
        unit.Components.Add(new UnitComponent { Type = "Parking", CoefficientPct = 0.2m, IsActive = false });

        // Act
        var total = unit.GetTotalCoefficientPct();

        // Assert
        Assert.Equal(0.8m, total);
    }

    [Fact]
    public void GetTotalCoefficientPct_WithNoActiveComponents_ShouldFallbackToLegacyCoefficient()
    {
        // Arrange
        var unit = new Unit { Number = "101", CoefficientPct = 1.25m };
        // Empty components list

        // Act
        var total = unit.GetTotalCoefficientPct();

        // Assert
        Assert.Equal(1.25m, total);
    }

    [Fact]
    public void GetTotalCoefficientPct_WithAllInactiveComponents_ShouldFallbackToLegacyCoefficient()
    {
        // Arrange
        var unit = new Unit { Number = "101", CoefficientPct = 1.25m };
        unit.Components.Add(new UnitComponent { Type = "Dept", CoefficientPct = 0.8m, IsActive = false });

        // Act
        var total = unit.GetTotalCoefficientPct();

        // Assert
        Assert.Equal(1.25m, total);
    }
}
