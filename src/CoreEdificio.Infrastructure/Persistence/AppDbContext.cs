using CoreEdificio.Domain.Entities;
using CoreEdificio.Domain.Entities.Billing;
using CoreEdificio.Domain.Entities.Payments;
using CoreEdificio.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace CoreEdificio.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public virtual DbSet<Community> Communities { get; set; } = null!;
    public virtual DbSet<Unit> Units { get; set; } = null!;
    public virtual DbSet<Expense> Expenses { get; set; } = null!;
    public virtual DbSet<BillingPeriod> BillingPeriods { get; set; } = null!;
    public virtual DbSet<UnitCharge> UnitCharges { get; set; } = null!;
    public virtual DbSet<Payment> Payments { get; set; } = null!;
    public virtual DbSet<UserUnit> UserUnits { get; set; } = null!;
    public virtual DbSet<UnitComponent> UnitComponents { get; set; } = null!;


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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

        modelBuilder.Entity<Payment>(b =>
        {
            b.ToTable("Payments");
            b.HasKey(x => x.Id);

            b.Property(x => x.CommunityId).IsRequired();
            b.Property(x => x.UnitId).IsRequired();

            b.Property(x => x.Period).HasMaxLength(7).IsRequired();

            b.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();

            b.Property(x => x.Method).IsRequired();
            b.Property(x => x.Reference).HasMaxLength(100);

            b.Property(x => x.PaidAtUtc).IsRequired();
            b.Property(x => x.CreatedAtUtc).IsRequired();

            b.HasIndex(x => new { x.CommunityId, x.UnitId, x.Period });
        });

        modelBuilder.Entity<UserUnit>(b =>
        {
            b.ToTable("UserUnits");
            b.HasKey(x => new { x.UserId, x.UnitId }); // Composite Key

            b.Property(x => x.RelationshipType).HasMaxLength(50).IsRequired();
            b.Property(x => x.CreatedAtUtc).IsRequired();

            // Relación con ApplicationUser
            b.HasOne<ApplicationUser>() 
             .WithMany(u => u.UserUnits)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            // Relación con Unit
            b.HasOne(x => x.Unit)
             .WithMany(u => u.UserUnits)
             .HasForeignKey(x => x.UnitId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(x => x.CommunityId);
        });

        modelBuilder.Entity<UnitComponent>(b =>
        {
            b.ToTable("UnitComponents");
            b.HasKey(x => x.Id);

            b.Property(x => x.Type).HasMaxLength(50).IsRequired();
            b.Property(x => x.Code).HasMaxLength(50).IsRequired();
            b.Property(x => x.CoefficientPct).HasPrecision(7, 4).IsRequired();
            b.Property(x => x.CreatedAtUtc).IsRequired();

            b.HasOne(x => x.Unit)
             .WithMany(u => u.Components)
             .HasForeignKey(x => x.UnitId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(x => new { x.CommunityId, x.UnitId });
            b.HasIndex(x => new { x.UnitId, x.Type, x.Code }).IsUnique();
        });
    }
}
