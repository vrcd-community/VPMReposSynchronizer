using System.Text.Json;
using System.Text.Json.Serialization;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Models.Types;
using VPMReposSynchronizer.Core.Options;
using VPMReposSynchronizer.Core.Services;
using VPMReposSynchronizer.Core.Services.FileHost;

namespace VPMReposSynchronizer.Entry.Controllers;

[ApiController]
[Route("vpm")]
[Produces("application/json")]
public class VpmRepoController(
    RepoMetaDataService repoMetaDataService,
    IFileHostService fileHostService,
    IOptions<MirrorRepoMetaDataOptions> options,
    IMapper mapper) : ControllerBase
{
    [HttpGet]
    [OutputCache(PolicyName = "vpm")]
    public async Task<JsonResult> Index()
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

        var repo = new VpmRepo(options.Value.RepoName, options.Value.RepoAuthor, options.Value.RepoUrl, options.Value.RepoId,
            packages);

        return new JsonResult(repo, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private async Task<VpmPackage> GetPackageWithUrl(VpmPackageEntity package)
    {
        var vpmPackage = mapper.Map<VpmPackage>(package);
        vpmPackage.Url = await fileHostService.GetFileUriAsync(package.FileId);

        return vpmPackage;
    }
}