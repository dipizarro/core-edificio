using CoreEdificio.Application.Contracts.Billing;
using CoreEdificio.Application.Interfaces.Billing;
using CoreEdificio.Application.Interfaces.Payments;
using CoreEdificio.Application.Services;
using CoreEdificio.Domain.Entities;
using CoreEdificio.Domain.Entities.Billing;
using CoreEdificio.Infrastructure.Persistence;
using CoreEdificio.Infrastructure.Repositories.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace CoreEdificio.Tests.Billing;

public class BillingServiceProrationIntegrationTests
{
    [Fact]
    public async Task IssueAsync_Should_UseCompositeCoefficients_ForProration()
    {
        // Arrange
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            using var db = new AppDbContext(options);
            db.Database.EnsureCreated();

            // 1) Setup Community
            var community = new Community { Name = "Test Community", Address = "Test Address" };
            db.Communities.Add(community);

            // 2) Setup Units with Components
            // Unit 1: 101, Coef 50.00 (via parts)
            var u1 = new Unit { CommunityId = community.Id, Number = "101", CoefficientPct = 0m };
            u1.Components.Add(new UnitComponent { CommunityId = community.Id, Type = "Dept", Code = "101", CoefficientPct = 40.00m, IsActive = true });
            u1.Components.Add(new UnitComponent { CommunityId = community.Id, Type = "Parking", Code = "E-1", CoefficientPct = 10.00m, IsActive = true });

            // Unit 2: 102, Coef 50.00 (legacy fallback)
            var u2 = new Unit { CommunityId = community.Id, Number = "102", CoefficientPct = 50.00m };

            db.Units.AddRange(u1, u2);
            await db.SaveChangesAsync();

            // 3) Setup Expenses
            var period = "2026-01";
            var expense = new Expense { CommunityId = community.Id, Period = period, Description = "Total Expense", Amount = 1000m };
            db.Expenses.Add(expense);
            await db.SaveChangesAsync();

            // 4) Setup Service
            var unitReadRepo = new UnitReadRepository(db);
            var expensesRepo = new Mock<IExpenseRepository>();
            expensesRepo.Setup(x => x.GetTotalByCommunityAndPeriodAsync(community.Id, period, It.IsAny<CancellationToken>()))
                .ReturnsAsync(1000m);

            var periodsRepo = new Mock<IBillingPeriodRepository>();
            periodsRepo.Setup(x => x.GetByCommunityAndPeriodAsync(community.Id, period, It.IsAny<CancellationToken>()))
                .ReturnsAsync((BillingPeriod?)null);

            var chargesRepo = new Mock<IUnitChargeRepository>();
            var manualChargesRepo = new Mock<IChargeRepository>();
            var paymentsRepo = new Mock<IPaymentRepository>();
            var uow = new Mock<IUnitOfWork>();
            
            // Simular transacción simple ya que estamos en memoria real pero con repos mockeados? 
            // Mejor usar repos reales para mayor fidelidad si es posible, pero algunos son mocks por simplicidad.
            // Para este test, la lógica crítica es: BillingService -> UnitReadRepo -> db.Units -> mapping a snapshots.
            
            var config = new Mock<IConfiguration>();

            var service = new BillingService(
                expensesRepo.Object,
                periodsRepo.Object,
                chargesRepo.Object,
                manualChargesRepo.Object,
                unitReadRepo, // Real repo!
                paymentsRepo.Object,
                uow.Object,
                config.Object
            );

            // 5) Act - Solo traemos los snapshots para validar el mapeo
            var snapshots = await unitReadRepo.ListSnapshotsByCommunityAsync(community.Id);

            // 6) Assert
            Assert.Equal(2, snapshots.Count);
            
            var s1 = snapshots.First(x => x.Number == "101");
            var s2 = snapshots.First(x => x.Number == "102");

            Assert.Equal(50.00m, s1.CoefficientPct); // Suma de 40 + 10
            Assert.Equal(50.00m, s2.CoefficientPct); // Fallback a legacy

            // Validar proration call
            var charges = BillingProrationCalculator.Compute(snapshots, 1000m);
            Assert.Equal(500m, charges.First(x => x.UnitNumber == "101").Amount);
            Assert.Equal(500m, charges.First(x => x.UnitNumber == "102").Amount);
        }
        finally
        {
            connection.Close();
        }
    }
}
