using System.Net.Http.Headers;
using System.Text.Json;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Models.Types;
using VPMReposSynchronizer.Core.Services;
using VPMReposSynchronizer.Core.Services.FileHost;

namespace VPMReposSynchronizer.Entry.Controllers;

[ApiController]
[Route("vpm")]
public class VpmRepoController(
    RepoMetaDataService repoMetaDataService,
    RepoSynchronizerService repoSynchronizerService,
    IFileHostService fileHostService,
    IMapper mapper) : ControllerBase
{
    [HttpGet]
    [Route("test")]
    public async Task<VpmPackageEntity[]> Test()
    {
        return await repoMetaDataService.GetVpmPackages();
    }

    [HttpPost]
    [Route("sync")]
    public async Task Sync()
    {
        await repoSynchronizerService.StartSync("https://packages.vrchat.com/official");
    }

    [HttpGet]
    [OutputCache(PolicyName = "vpm")]
    public async Task<VpmRepo> Index()
    {
        var vpmPackageEntities = await repoMetaDataService.GetVpmPackages();

        var packages =
            vpmPackageEntities
                .GroupBy(package => package.Name)
                .Select(package =>
                    package.Select(async version =>
                            new KeyValuePair<string, VpmPackage>(version.Version,await GetPackageWithUrl(version)))
                        .Select(task => new KeyValuePair<string, VpmPackage>(task.Result.Key, task.Result.Value))
                        .ToDictionary())
                .Select(packageVersions => new KeyValuePair<string, VpmRepoPackageVersions>(packageVersions.First().Value.Name,
                    new VpmRepoPackageVersions(packageVersions)))
                .ToDictionary();

        var repo = new VpmRepo("Official Mirror", "VRChat", "https://example.com", "com.vrchat.repos.official.mirror",
            packages);

        return repo;
    }

    private async Task<VpmPackage> GetPackageWithUrl(VpmPackageEntity package)
    {
        var vpmPackage = mapper.Map<VpmPackage>(package);
        vpmPackage.Url = await fileHostService.GetFileUriAsync(package.FileId);

        return vpmPackage;
    }
}