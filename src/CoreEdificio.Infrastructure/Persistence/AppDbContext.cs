using CoreEdificio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Community> Communities => Set<Community>();
    public DbSet<Unit> Units => Set<Unit>();


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

    }
}
