using Microsoft.EntityFrameworkCore;
using VPMReposSynchronizer.Core.Models.Entity;

namespace VPMReposSynchronizer.Core.DbContexts;

public class DefaultDbContext : DbContext
{
    public DefaultDbContext(DbContextOptions<DefaultDbContext> options) : base(options)
    {
    }

    public DbSet<VpmPackageEntity> Packages { get; set; }
    public DbSet<S3FileRecordEntity> S3FileRecords { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VpmPackageEntity>().ToTable("Packages");
        modelBuilder.Entity<S3FileRecordEntity>().ToTable("S3FileRecords");
    }
}