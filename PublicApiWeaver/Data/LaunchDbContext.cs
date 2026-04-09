using Microsoft.EntityFrameworkCore;
using PublicApiWeaver.Models;

namespace PublicApiWeaver.Data;

public sealed class LaunchDbContext(DbContextOptions<LaunchDbContext> options) : DbContext(options)
{
    public DbSet<SpaceLaunch> Launches => Set<SpaceLaunch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SpaceLaunch>(entity =>
        {
            entity.HasIndex(x => x.ExternalId).IsUnique();
            entity.Property(x => x.ExternalId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.MissionName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Launchpad).HasMaxLength(128);
            entity.Property(x => x.WebcastUrl).HasMaxLength(500);
        });
    }
}