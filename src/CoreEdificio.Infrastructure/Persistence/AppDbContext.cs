using CoreEdificio.Domain.Entities;
using CoreEdificio.Domain.Entities.Billing;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Community> Communities => Set<Community>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<BillingPeriod> BillingPeriods => Set<BillingPeriod>();
    public DbSet<UnitCharge> UnitCharges => Set<UnitCharge>();



    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Community>(b =>
        {
            b.ToTable("Communities");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Address).HasMaxLength(300).IsRequired();
            b.Property(x => x.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<Unit>(b =>
        {
            b.ToTable("Units");
            b.HasKey(x => x.Id);

            b.Property(x => x.Number).HasMaxLength(20).IsRequired();
            b.Property(x => x.CoefficientPct).HasPrecision(7, 4).IsRequired();

            b.Property(x => x.OwnerName).HasMaxLength(120);
            b.Property(x => x.OwnerEmail).HasMaxLength(200);

            b.Property(x => x.CreatedAtUtc).IsRequired();

            b.HasOne(x => x.Community)
                .WithMany(c => c.Units)
                .HasForeignKey(x => x.CommunityId)
                .OnDelete(DeleteBehavior.Cascade);

            // Un número de unidad no se repite dentro de una comunidad
            b.HasIndex(x => new { x.CommunityId, x.Number }).IsUnique();
        });

        modelBuilder.Entity<Expense>(b =>
        {
            b.ToTable("Expenses");
            b.HasKey(x => x.Id);

            b.Property(x => x.CommunityId).IsRequired();
            b.Property(x => x.Period).HasMaxLength(7).IsRequired(); // YYYY-MM

            b.Property(x => x.Description).HasMaxLength(250).IsRequired();
            b.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();

            b.Property(x => x.CreatedAtUtc).IsRequired();

            b.HasIndex(x => new { x.CommunityId, x.Period });
        });

        modelBuilder.Entity<BillingPeriod>(b =>
        {
            b.ToTable("BillingPeriods");
            b.HasKey(x => x.Id);

            b.Property(x => x.CommunityId).IsRequired();
            b.Property(x => x.Period).HasMaxLength(7).IsRequired();

            b.Property(x => x.Status).IsRequired();

            b.Property(x => x.TotalExpenses).HasPrecision(18, 2).IsRequired();
            b.Property(x => x.TotalCoefficientPct).HasPrecision(18, 4).IsRequired();

            b.Property(x => x.IssuedAtUtc);

            // Un período por comunidad (único)
            b.HasIndex(x => new { x.CommunityId, x.Period }).IsUnique();
        });

        modelBuilder.Entity<UnitCharge>(b =>
        {
            b.ToTable("UnitCharges");
            b.HasKey(x => x.Id);

            b.Property(x => x.BillingPeriodId).IsRequired();
            b.Property(x => x.UnitId).IsRequired();

            b.Property(x => x.CoefficientPct).HasPrecision(7, 4).IsRequired();
            b.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();

            b.HasOne(x => x.BillingPeriod)
                .WithMany(p => p.UnitCharges)
                .HasForeignKey(x => x.BillingPeriodId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(x => new { x.BillingPeriodId, x.UnitId }).IsUnique();
        });
        

    }
}
