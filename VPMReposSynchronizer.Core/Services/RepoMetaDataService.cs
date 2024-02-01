using AutoMapper;
using Microsoft.EntityFrameworkCore;
using VPMReposSynchronizer.Core.DbContexts;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Models.Types;

namespace VPMReposSynchronizer.Core.Services;

public class RepoMetaDataService(DefaultDbContext defaultDbContext, IMapper mapper)
{
    public async Task AddVpmPackageAsync(VpmPackageEntity vpmPackageEntity)
    {
        defaultDbContext.Packages.Add(vpmPackageEntity);
        await defaultDbContext.SaveChangesAsync();
    }

    public async Task AddVpmPackageAsync(VpmPackage vpmPackage, string fileId)
    {
        var entity = mapper.Map<VpmPackageEntity>(vpmPackage);
        entity.FileId = fileId;

        await AddVpmPackageAsync(entity);
    }

    public async Task AddOrUpdateVpmPackageAsync(VpmPackageEntity vpmPackageEntity)
    {
        if (await defaultDbContext.Packages.AnyAsync(package => package.PackageId == vpmPackageEntity.PackageId))
        {
            defaultDbContext.Packages.Update(vpmPackageEntity);
        }
        else
        {
            defaultDbContext.Packages.Add(vpmPackageEntity);
        }

        await defaultDbContext.SaveChangesAsync();
    }

    public async Task AddOrUpdateVpmPackageAsync(VpmPackage vpmPackage, string fileId, string repoId,
        string originRepoId)
    {
        var entity = mapper.Map<VpmPackageEntity>(vpmPackage);
        entity.FileId = fileId;
        entity.UpstreamId = repoId;
        entity.UpstreamOriginId = originRepoId;

        await AddOrUpdateVpmPackageAsync(entity);
    }

    public async Task AddRepoAsync(VpmRepoEntity vpmRepoEntity)
    {
        defaultDbContext.Repos.Add(vpmRepoEntity);
        await defaultDbContext.SaveChangesAsync();
    }

    public async Task AddRepoAsync(VpmRepo vpmRepo, string configurationId)
    {
        var entity = mapper.Map<VpmRepoEntity>(vpmRepo);
        entity.ConfigurationId = configurationId;

        defaultDbContext.Repos.Add(entity);
        await defaultDbContext.SaveChangesAsync();
    }

    public async Task AddRepoAsync(VpmRepo vpmRepo, string configurationId, string upstreamUrl)
    {
        var entity = mapper.Map<VpmRepoEntity>(vpmRepo);
        entity.ConfigurationId = configurationId;
        entity.UpStreamUrl = upstreamUrl;

        defaultDbContext.Repos.Add(entity);
        await defaultDbContext.SaveChangesAsync();
    }

    public async Task AddOrUpdateRepoAsync(VpmRepoEntity vpmRepoEntity)
    {
        if (await defaultDbContext.Repos.AnyAsync(package => package.Id == vpmRepoEntity.Id))
        {
            defaultDbContext.Repos.Update(vpmRepoEntity);
        }
        else
        {
            defaultDbContext.Repos.Add(vpmRepoEntity);
        }

        await defaultDbContext.SaveChangesAsync();
    }

    public async Task AddOrUpdateRepoAsync(VpmRepo vpmRepo, string configurationId, string upstreamUrl)
    {
        var entity = mapper.Map<VpmRepoEntity>(vpmRepo);

        if (vpmRepo.Id is null)
            entity.Id = configurationId;

        entity.ConfigurationId = configurationId;
        entity.UpStreamUrl = upstreamUrl;

        await AddOrUpdateRepoAsync(entity);
    }

    public async Task<VpmRepoEntity[]> GetAllRepos()
    {
        return await defaultDbContext.Repos.ToArrayAsync();
    }

    public async Task<VpmRepoEntity?> GetRepoByConfigurationId(string id)
    {
        return await defaultDbContext.Repos.FirstOrDefaultAsync(entity => entity.ConfigurationId == id);
    }

    public async Task<VpmPackageEntity[]> GetVpmPackages()
    {
        return await defaultDbContext.Packages.ToArrayAsync();
    }

    public async Task<VpmPackageEntity?> GetVpmPackage(string packageName, string packageVersion)
    {
        return await defaultDbContext.Packages.AsNoTracking()
            .FirstAsync(package => package.PackageId == $"{packageName}@{packageVersion}");
    }

    public async Task<VpmPackageEntity[]> GetVpmPackages(string repo)
    {
        return await defaultDbContext.Packages.Where(package => package.UpstreamId == repo).ToArrayAsync();
    }

    public async Task<VpmPackageEntity[]> SearchVpmPackages(string keyword)
    {
        // ReSharper disable once SimplifyConditionalTernaryExpression
        return await defaultDbContext.Packages
            .Where(package =>
                package.Name.Contains(keyword) ||
                (package.DisplayName != null && package.DisplayName.Contains(keyword)) ||
                (package.Description != null && package.Description.Contains(keyword)))
            .ToArrayAsync();
    }
}