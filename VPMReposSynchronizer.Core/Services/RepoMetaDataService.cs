using AutoMapper;
using Microsoft.EntityFrameworkCore;
using VPMReposSynchronizer.Core.DbContexts;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Models.Types;
using VPMReposSynchronizer.Core.Services.RepoSync;

namespace VPMReposSynchronizer.Core.Services;

public class RepoMetaDataService(
    DefaultDbContext defaultDbContext,
    RepoSyncTaskScheduleService repoSyncTaskScheduleService,
    IMapper mapper)
{
    #region VpmPacakge

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

    public async Task MarkAddOrUpdateVpmPackageAsync(VpmPackageEntity vpmPackageEntity)
    {
        if (await defaultDbContext.Packages.AnyAsync(package => package.PackageId == vpmPackageEntity.PackageId))
            defaultDbContext.Packages.Update(vpmPackageEntity);
        else
            defaultDbContext.Packages.Add(vpmPackageEntity);
    }

    public async Task MarkAddOrUpdateVpmPackageAsync(VpmPackage vpmPackage, string fileId, string repoId)
    {
        var entity = mapper.Map<VpmPackageEntity>(vpmPackage);
        entity.PackageId = $"{vpmPackage.Name}@{vpmPackage.Version}@{repoId}";
        entity.FileId = fileId;
        entity.UpstreamId = repoId;

        await MarkAddOrUpdateVpmPackageAsync(entity);
    }

    /// <summary>
    /// 删除指定仓库中不在上游清单内的包版本记录，使镜像与上游清单保持一致。
    /// 只删除元数据：文件按内容哈希寻址，仍可能被其它仓库或版本引用，因此不在此处删除。
    /// 记录只按 <paramref name="repoId"/> 过滤，同一版本来自其它上游的记录不受影响。
    /// </summary>
    /// <returns>标记删除的记录数；由调用方统一保存。</returns>
    public async Task<int> RemoveVpmPackagesNotInAsync(string repoId,
        IReadOnlySet<(string PackageName, string Version)> keepPackages)
    {
        var mirroredPackages = await defaultDbContext.Packages
            .Where(package => package.UpstreamId == repoId)
            .ToArrayAsync();

        var removedPackages = mirroredPackages
            .Where(package => !keepPackages.Contains((package.Name, package.Version)))
            .ToArray();

        defaultDbContext.Packages.RemoveRange(removedPackages);

        return removedPackages.Length;
    }

    public async Task<VpmPackageEntity[]> GetVpmPackages()
    {
        return await defaultDbContext.Packages.ToArrayAsync();
    }

    public async Task<VpmPackageEntity?> GetVpmPackage(string packageName, string packageVersion, string repoId)
    {
        return await defaultDbContext.Packages.AsNoTracking()
            .FirstOrDefaultAsync(package => package.PackageId == $"{packageName}@{packageVersion}@{repoId}");
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

    #endregion

    #region VpmRepo

    public async Task AddRepoAsync(VpmRepoEntity vpmRepoEntity)
    {
        defaultDbContext.Repos.Add(vpmRepoEntity);
        await defaultDbContext.SaveChangesAsync();

        await repoSyncTaskScheduleService.ScheduleAllTasks();
    }

    public async Task UpdateRepoAsync(string id, VpmRepo vpmRepo)
    {
        var entity = await GetRepoById(id);

        if (entity is null)
            throw new InvalidOperationException("Repo not found.");

        entity.OriginalRepoId = vpmRepo.Id;
        entity.Name = vpmRepo.Name;
        entity.Author = vpmRepo.Author;

        await defaultDbContext.SaveChangesAsync();
    }

    public async Task UpdateRepoAsync(VpmRepoEntity vpmRepoEntity)
    {
        defaultDbContext.Repos.Update(vpmRepoEntity);
        await defaultDbContext.SaveChangesAsync();

        await repoSyncTaskScheduleService.ScheduleAllTasks();
    }

    public async Task DeleteRepoAsync(string id)
    {
        var repo = await GetRepoById(id);
        if (repo is null) throw new InvalidOperationException("Repo not found.");

        defaultDbContext.Repos.Remove(repo);
        await defaultDbContext.SaveChangesAsync();

        await repoSyncTaskScheduleService.ScheduleAllTasks();
    }

    public async Task<VpmRepoEntity[]> GetRepos()
    {
        return await defaultDbContext.Repos.ToArrayAsync();
    }

    public async Task<VpmRepoEntity?> GetRepoById(string id)
    {
        return await defaultDbContext.Repos.FindAsync(id);
    }

    public async Task<bool> IsRepoExist(string id)
    {
        return await defaultDbContext.Repos.AnyAsync(repo => repo.Id == id);
    }

    #endregion
}
