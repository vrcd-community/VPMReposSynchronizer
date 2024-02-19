using AutoMapper;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Models.Types;
using VPMReposSynchronizer.Core.Models.Types.RepoBrowser;
using VPMReposSynchronizer.Core.Options;

namespace VPMReposSynchronizer.Core.Services;

public class RepoBrowserService(
    RepoMetaDataService repoMetaDataService,
    RepoSynchronizerStatusService repoSynchronizerStatusService,
    IOptions<MirrorRepoMetaDataOptions> options,
    IOptions<FileHostServiceOptions> fileHostOptions,
    IMapper mapper)
{
    public async ValueTask<BrowserRepo[]> GetAllReposAsync()
    {
        var repoEntities = await repoMetaDataService.GetAllRepos();

        return repoEntities.Select(entity => new BrowserRepo(
                ApiId: entity.ConfigurationId,
                Name: entity.Name,
                UpstreamId: entity.Id,
                UpstreamUrl: entity.UpStreamUrl,
                Author: entity.Author,
                RepoUrl: GetRepoUrl(entity.ConfigurationId),
                SyncStatus: new SyncStatusPublic(entity.ConfigurationId,
                    repoSynchronizerStatusService.SyncStatus[entity.ConfigurationId])))
            .ToArray();
    }

    public async ValueTask<BrowserPackage[]?> GetAllPackagesAsync(string repoId)
    {
        var packageEntities = await repoMetaDataService.GetVpmPackages(repoId);
        var packages = packageEntities
            .Select(GetPackageWithUrl)
            .GroupBy(package => package.Name)
            .Select(packagesGroup =>
                new BrowserPackage(Latest: packagesGroup.First(), Versions: packagesGroup.ToArray(), RepoId: repoId,
                    RepoUrl: GetRepoUrl(repoId)));

        return packages.ToArray();
    }

    public async ValueTask<BrowserPackage[]> SearchVpmPackages(string keyword)
    {
        var packagesEntities = await repoMetaDataService.SearchVpmPackages(keyword);

        // packagesEntities
        //     .GroupBy(package => package.Name)
        //     .Select()

        var packagesGroup = packagesEntities
            .GroupBy(package => package.Name)
            .ToArray();

        return packagesGroup.Select((group, index) =>
                new BrowserPackage(Latest: GetPackageWithUrl(group.First()), Versions: group.Select(GetPackageWithUrl).ToArray(),
                    RepoId: packagesGroup[index].Select(pkg => pkg.UpstreamId).First(),
                    RepoUrl: GetRepoUrl(packagesGroup[index].Select(pkg => pkg.UpstreamId).First())))
            .ToArray();
    }

    public async ValueTask<BrowserRepo?> GetRepoAsync(string id)
    {
        var repoEntity = await repoMetaDataService.GetRepoByConfigurationId(id);

        if (repoEntity is null)
            return null;

        return new BrowserRepo(
            ApiId: repoEntity.ConfigurationId,
            Name: repoEntity.Name,
            UpstreamId: repoEntity.Id,
            UpstreamUrl: repoEntity.UpStreamUrl,
            Author: repoEntity.Author,
            RepoUrl: GetRepoUrl(id),
            SyncStatus: new SyncStatusPublic(repoEntity.ConfigurationId,
                repoSynchronizerStatusService.SyncStatus[repoEntity.ConfigurationId]));
    }

    public async ValueTask<BrowserPackage?> GetPackageAsync(string repoId, string packageName)
    {
        var packageEntities = await repoMetaDataService.GetVpmPackages(repoId);

        var packages = packageEntities
            .Select(GetPackageWithUrl)
            .Where(package => package.Name == packageName)
            .Select(package => package)
            .ToArray();

        return packages.Length == 0
            ? null
            : new BrowserPackage(Latest: packages[0], Versions: packages, RepoId: repoId,
                RepoUrl: GetRepoUrl(repoId));
    }

    private string GetRepoUrl(string id)
    {
        return options.Value.RepoUrl.Replace("{id}", id);
    }

    private VpmPackage GetPackageWithUrl(VpmPackageEntity package)
    {
        var vpmPackage = mapper.Map<VpmPackage>(package);

        var fileDownloadEndpoint = new Uri(fileHostOptions.Value.BaseUrl,
            $"files/download/{package.UpstreamId}@{package.PackageId}.zip").ToString();
        vpmPackage.Url = QueryHelpers.AddQueryString(fileDownloadEndpoint, "fileId", package.FileId);

        return vpmPackage;
    }
}