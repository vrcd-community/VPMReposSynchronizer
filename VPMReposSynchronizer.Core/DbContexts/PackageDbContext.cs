using Microsoft.EntityFrameworkCore;
using VPMReposSynchronizer.Core.Models.Entity;

namespace VPMReposSynchronizer.Core.DbContexts;

public class PackageDbContext : DbContext
{
    public PackageDbContext(DbContextOptions<PackageDbContext> options) : base(options)
    {
    }

    public DbSet<VpmPackageEntity> Packages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VpmPackageEntity>().ToTable("Packages");
    }
}