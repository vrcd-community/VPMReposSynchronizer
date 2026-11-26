using AutoMapper;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Semver;
using VPMReposSynchronizer.Core.Extensions;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Models.Types;
using VPMReposSynchronizer.Core.Models.Types.RepoBrowser;
using VPMReposSynchronizer.Core.Options;

namespace VPMReposSynchronizer.Core.Services;

public class RepoBrowserService(
    RepoMetaDataService repoMetaDataService,
    RepoSyncStatusService repoSyncStatusService,
    IOptions<MirrorRepoMetaDataOptions> options,
    IOptions<FileHostServiceOptions> fileHostOptions,
    IMapper mapper)
{
    public async ValueTask<PageResult<BrowserRepo>> GetReposAsync(int page, int count)
    {
        var repoEntities = await repoMetaDataService.GetRepos();
        var repoSyncStatuses = (await repoSyncStatusService.GetAllSyncStatusAsync())
            .ToDictionary(status => status.RepoId, status => status);

        var browserRepos = mapper.Map<BrowserRepo[]>(repoEntities);
        foreach (var browserRepo in browserRepos)
        {
            browserRepo.SyncStatus = repoSyncStatuses.TryGetValue(browserRepo.ApiId, out var syncStatus)
                ? syncStatus
                : null;
            browserRepo.RepoUrl = GetRepoUrl(browserRepo.ApiId);
        }

        return browserRepos.ToPageResult(page, count);
    }

    public async ValueTask<PageResult<BrowserPackage>> GetPackagesAsync(string repoId, int count, int page)
    {
        var packageEntities = await repoMetaDataService.GetVpmPackages(repoId);
        var packageGroups = packageEntities
            .Select(GetPackageWithUrl)
            .GroupBy(package => package.Name)
            .ToArray();

        var packages = packageGroups
            .Skip(page * count)
            .Take(count)
            .Select(package => OrderBySemanticVersionDescending(package, pkg => pkg.Version))
            .Select(packagesGroup =>
                new BrowserPackage(packagesGroup.First(), packagesGroup.ToArray(), repoId,
                    GetRepoUrl(repoId)));

        return new PageResult<BrowserPackage>(packages.ToArray(), packageGroups.Length);
    }

    public async ValueTask<BrowserPackage[]> SearchVpmPackages(string keyword)
    {
        var packagesEntities = await repoMetaDataService.SearchVpmPackages(keyword);

        var packagesGroup = packagesEntities
            .GroupBy(package => package.Name)
            .ToArray();

        return packagesGroup.Select((group, index) =>
            {
                var versions = OrderBySemanticVersionDescending(group.Select(GetPackageWithUrl),
                    package => package.Version).ToArray();

                return new BrowserPackage(versions[0], versions,
                    packagesGroup[index].Select(pkg => pkg.UpstreamId).First(),
                    GetRepoUrl(packagesGroup[index].Select(pkg => pkg.UpstreamId).First()));
            })
            .ToArray();
    }

    public async ValueTask<BrowserRepo?> GetRepoAsync(string id)
    {
        var repoEntity = await repoMetaDataService.GetRepoById(id);

        if (repoEntity is null) return null;

        var syncStatus = await repoSyncStatusService.GetSyncStatusAsync(id);

        var browserRepo = mapper.Map<BrowserRepo>(repoEntity);

        browserRepo.SyncStatus = syncStatus;
        browserRepo.RepoUrl = GetRepoUrl(id);

        return browserRepo;
    }

    public async ValueTask<BrowserPackage?> GetPackageAsync(string repoId, string packageName)
    {
        var packageEntities = await repoMetaDataService.GetVpmPackages(repoId);

        var packages = OrderBySemanticVersionDescending(
                packageEntities
                    .Select(GetPackageWithUrl)
                    .Where(package => package.Name == packageName),
                package => package.Version)
            .ToArray();

        return packages.Length == 0
            ? null
            : new BrowserPackage(packages[0], packages, repoId,
                GetRepoUrl(repoId));
    }

    private string GetRepoUrl(string id)
    {
        return options.Value.RepoUrl.Replace("{id}", id);
    }

    /// <summary>
    ///     按语义化版本降序排序。数据库中的版本字符串来自上游清单，可能存在无法解析的取值，
    ///     无法解析的版本排在末尾，彼此保持原有顺序。
    /// </summary>
    internal static List<T> OrderBySemanticVersionDescending<T>(
        IEnumerable<T> source, Func<T, string> versionSelector)
    {
        var parsed = source
            .Select(item =>
            {
                var canParse = SemVersion.TryParse(versionSelector(item), SemVersionStyles.Any, out var version);
                return (Item: item, Version: canParse ? version : null);
            })
            .ToArray();

        return parsed
            .Where(entry => entry.Version is not null)
            .OrderByDescending(entry => entry.Version!, SemVersion.SortOrderComparer)
            .Select(entry => entry.Item)
            .Concat(parsed.Where(entry => entry.Version is null).Select(entry => entry.Item))
            .ToList();
    }

    private VpmPackage GetPackageWithUrl(VpmPackageEntity package)
    {
        var vpmPackage = mapper.Map<VpmPackage>(package);

        var fileDownloadEndpoint = new Uri(fileHostOptions.Value.BaseUrl,
            $"files/download/{package.PackageId}.zip").ToString();
        vpmPackage.Url = QueryHelpers.AddQueryString(fileDownloadEndpoint, "fileId", package.FileId);

        return vpmPackage;
    }
}
