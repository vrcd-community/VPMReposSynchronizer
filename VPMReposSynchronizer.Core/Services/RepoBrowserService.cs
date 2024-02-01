﻿using AutoMapper;
using Microsoft.Extensions.Options;
using VPMReposSynchronizer.Core.Models.Types;
using VPMReposSynchronizer.Core.Models.Types.RepoBrowser;
using VPMReposSynchronizer.Core.Options;

namespace VPMReposSynchronizer.Core.Services;

public class RepoBrowserService(
    RepoMetaDataService repoMetaDataService,
    RepoSynchronizerStatusService repoSynchronizerStatusService,
    IOptions<MirrorRepoMetaDataOptions> options,
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
        var packages = mapper.Map<VpmPackage[]>(packageEntities)
            .GroupBy(package => package.Name)
            .Select(packagesGroup =>
                new BrowserPackage(Latest: packagesGroup.First(), Versions: packagesGroup.ToArray(), RepoId: repoId, RepoUrl: GetRepoUrl(repoId)));

        return packages.ToArray();
    }

    public async ValueTask<BrowserPackage[]> SearchVpmPackages(string keyword)
    {
        var packagesEntities = await repoMetaDataService.SearchVpmPackages(keyword);

        var packages = mapper.Map<VpmPackage[]>(packagesEntities);

        return packages
            .GroupBy(package => package.Name)
            .Select((packagesGroup, index) =>
                new BrowserPackage(Latest: packagesGroup.First(), Versions: packagesGroup.ToArray(),
                    RepoId: packagesEntities[index].UpstreamId,
                    RepoUrl: GetRepoUrl(packagesEntities[index].UpstreamId)))
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

        var packages = mapper.Map<VpmPackage[]>(packageEntities)
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
}