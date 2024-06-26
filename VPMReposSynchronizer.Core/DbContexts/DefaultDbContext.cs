﻿using Microsoft.EntityFrameworkCore;
using VPMReposSynchronizer.Core.Models.Entity;

namespace VPMReposSynchronizer.Core.DbContexts;

public class DefaultDbContext : DbContext
{
    public DefaultDbContext(DbContextOptions<DefaultDbContext> options) : base(options)
    {
    }

    public DbSet<VpmPackageEntity> Packages { get; set; }
    public DbSet<S3FileRecordEntity> S3FileRecords { get; set; }
    public DbSet<VpmRepoEntity> Repos { get; set; }
    public DbSet<SyncTaskEntity> SyncTasks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VpmPackageEntity>().ToTable("Packages");
        modelBuilder.Entity<S3FileRecordEntity>().ToTable("S3FileRecords");
        modelBuilder.Entity<VpmRepoEntity>().ToTable("Repos");
        modelBuilder.Entity<SyncTaskEntity>().ToTable("SyncTasks");
    }
}