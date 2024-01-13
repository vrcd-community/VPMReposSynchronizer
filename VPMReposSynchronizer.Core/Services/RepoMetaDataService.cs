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

    public async Task AddOrUpdateVpmPackageAsync(VpmPackage vpmPackage, string fileId, string repoId)
    {
        var entity = mapper.Map<VpmPackageEntity>(vpmPackage);
        entity.FileId = fileId;
        entity.UpstreamId = repoId;

        await AddOrUpdateVpmPackageAsync(entity);
    }

    public async Task<VpmPackageEntity[]> GetVpmPackages()
    {
        return await defaultDbContext.Packages.ToArrayAsync();
    }

    public async Task<VpmPackageEntity[]> GetVpmPackages(string repo)
    {
        return await defaultDbContext.Packages.Where(package => package.UpstreamId == repo).ToArrayAsync();
    }
}