using AutoMapper;
using Microsoft.EntityFrameworkCore;
using VPMReposSynchronizer.Core.DbContexts;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Models.Types;

namespace VPMReposSynchronizer.Core.Services;

public class RepoMetaDataService(PackageDbContext packageDbContext, IMapper mapper)
{
    public async Task AddVpmPackageAsync(VpmPackageEntity vpmPackageEntity)
    {
        packageDbContext.Packages.Add(vpmPackageEntity);
        await packageDbContext.SaveChangesAsync();
    }

    public async Task AddVpmPackageAsync(VpmPackage vpmPackage, string fileId)
    {
        var entity = mapper.Map<VpmPackageEntity>(vpmPackage);
        entity.FileId = fileId;

        await AddVpmPackageAsync(entity);
    }

    public async Task AddOrUpdateVpmPackageAsync(VpmPackageEntity vpmPackageEntity)
    {
        if (await packageDbContext.Packages.AnyAsync(package => package.PackageId == vpmPackageEntity.PackageId))
        {
            packageDbContext.Packages.Update(vpmPackageEntity);
        }
        else
        {
            packageDbContext.Packages.Add(vpmPackageEntity);
        }

        await packageDbContext.SaveChangesAsync();
    }

    public async Task AddOrUpdateVpmPackageAsync(VpmPackage vpmPackage, string fileId)
    {
        var entity = mapper.Map<VpmPackageEntity>(vpmPackage);
        entity.FileId = fileId;

        await AddOrUpdateVpmPackageAsync(entity);
    }

    public async Task<VpmPackageEntity[]> GetVpmPackages()
    {
        return await packageDbContext.Packages.ToArrayAsync();
    }
}