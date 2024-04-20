using AutoMapper;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Semver;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Models.Types;
using VPMReposSynchronizer.Core.Models.Types.RepoBrowser;
using VPMReposSynchronizer.Core.Options;

namespace VPMReposSynchronizer.Core.Services;

public class RepoBrowserService(
    RepoMetaDataService repoMetaDataService,
    IOptions<MirrorRepoMetaDataOptions> options,
    IOptions<FileHostServiceOptions> fileHostOptions,
    IMapper mapper)
{
    public async ValueTask<BrowserRepo[]> GetAllReposAsync()
    {
        var repoEntities = await repoMetaDataService.GetAllRepos();

        return mapper.Map<BrowserRepo[]>(repoEntities);
    }

    public async ValueTask<BrowserPackage[]?> GetAllPackagesAsync(string repoId)
    {
        var packageEntities = await repoMetaDataService.GetVpmPackages(repoId);
        var packages = packageEntities
            .Select(GetPackageWithUrl)
            .GroupBy(package => package.Name)
            .Select(package => package.OrderByDescending(pkg => pkg.Version, SemVersion.SortOrderComparer))
            .Select(packagesGroup =>
                new BrowserPackage(Latest: packagesGroup.First(), Versions: packagesGroup.ToArray(), RepoId: repoId,
                    RepoUrl: GetRepoUrl(repoId)));

        return packages.ToArray();
    }

    public async ValueTask<BrowserPackage[]> SearchVpmPackages(string keyword)
    {
        var packagesEntities = await repoMetaDataService.SearchVpmPackages(keyword);

        var packagesGroup = packagesEntities
            .GroupBy(package => package.Name)
            .ToArray();

        return packagesGroup.Select((group, index) =>
            {
                var versions = group.Select(GetPackageWithUrl)
                    .OrderByDescending(package => package.Version, SemVersion.SortOrderComparer).ToArray();

                return new BrowserPackage(Latest: versions[0], Versions: versions,
                    RepoId: packagesGroup[index].Select(pkg => pkg.UpstreamId).First(),
                    RepoUrl: GetRepoUrl(packagesGroup[index].Select(pkg => pkg.UpstreamId).First()));
            })
            .ToArray();
    }

    public async ValueTask<BrowserRepo?> GetRepoAsync(string id)
    {
        var repoEntity = await repoMetaDataService.GetRepoById(id);

        if (repoEntity is null)
            return null;

        var browserRepo = mapper.Map<BrowserRepo?>(repoEntity);
        return browserRepo;
    }

    public async ValueTask<BrowserPackage?> GetPackageAsync(string repoId, string packageName)
    {
        var packageEntities = await repoMetaDataService.GetVpmPackages(repoId);

        var packages = packageEntities
            .Select(GetPackageWithUrl)
            .Where(package => package.Name == packageName)
            .Select(package => package)
            .OrderByDescending(package => package.Version, SemVersion.SortOrderComparer)
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