using CoreEdificio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Community> Communities => Set<Community>();

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
    }
}
