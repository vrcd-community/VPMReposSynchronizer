using System.Text.Json;
using System.Text.Json.Serialization;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Semver;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Models.Types;
using VPMReposSynchronizer.Core.Options;
using VPMReposSynchronizer.Core.Services;
using VPMReposSynchronizer.Core.Services.FileHost;

namespace VPMReposSynchronizer.Entry.Controllers;

/// <summary>
/// VPM Repos Controller
/// </summary>
[ApiController]
[Route("vpm")]
[Produces("application/json")]
[OutputCache(PolicyName = "vpm")]
public class VpmRepoController(
    RepoMetaDataService repoMetaDataService,
    IOptions<MirrorRepoMetaDataOptions> options,
    IOptions<FileHostServiceOptions> fileHostOptions,
    IMapper mapper) : ControllerBase
{
    /// <summary>
    /// Get all upstream repos.
    /// </summary>
    /// <returns>All upstream repos</returns>
    /// <remarks>
    /// Sample Response:
    ///
    ///     {
    ///         "curated": "https://packages.vrchat.com/curated"
    ///     }
    ///
    /// </remarks>
    /// <response code="200">Returns all upstream repos</response>
    [HttpGet]
    [Route("repos")]
    [ProducesResponseType<Dictionary<string, string>>(StatusCodes.Status200OK)]
    public async Task<Dictionary<string, string>> GetRepoLists()
    {
        var repos = await repoMetaDataService.GetAllRepos();
        return repos.Select(repo => new KeyValuePair<string, string>(repo.Id, repo.UpStreamUrl)).ToDictionary();
    }

    /// <summary>
    ///  Get a specific repo.
    /// </summary>
    /// <param name="repoId">Repo Id</param>
    /// <returns>Specific vpm repo</returns>
    /// <remarks>
    /// Sample Response:
    ///
    ///     {
    ///         "name": "curated@Local Debug VPM Repo",
    ///         "author": "Nameless",
    ///         "url": "http://localhost:5218/",
    ///         "id": "local.debug.vpm.repo.curated",
    ///         "packages": {
    ///             "com.llealloo.audiolink": {
    ///                 "versions": {
    ///                     "1.2.1": {
    ///                         "name": "com.llealloo.audiolink",
    ///                         "displayName": "AudioLink",
    ///                         "version": "1.2.1",
    ///                         "unity": "2022.3",
    ///                         "description": "Audio reactive prefabs for VRChat",
    ///                         "url": "http://localhost:5218/files/32a21337cf828c516fd3da20945ddbe973db3a625fbd805be9d37eda7c462e72/com.llealloo.audiolink-1.2.1.zip",
    ///                         "zipSHA256": "32a21337cf828c516fd3da20945ddbe973db3a625fbd805be9d37eda7c462e72",
    ///                         "legacyPackages": [],
    ///                         "legacyFolders": {
    ///                             "Assets\\AudioLink": ""
    ///                         },
    ///                         "legacyFiles": {},
    ///                         "dependencies": {},
    ///                         "gitDependencies": {},
    ///                         "vpmDependencies": {},
    ///                         "keywords": [],
    ///                         "samples": [
    ///                         {
    ///                             "displayName": "AudioLinkExampleScene",
    ///                             "description": "An example scene showing off the capabilities of AudioLink.",
    ///                             "path": "Samples~/AudioLinkExampleScene"
    ///                         }
    ///                         ],
    ///                         "headers": {}
    ///                     }
    ///                 }
    ///             }
    ///         }
    ///     }
    ///
    /// </remarks>
    /// <response code="200">Specific vpm repo</response>
    /// <response code="404">Repo not found</response>
    [HttpGet]
    [Route("{repoId}")]
    [OutputCache(PolicyName = "vpm")]
    [ProducesResponseType<VpmRepo>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<JsonResult> GetRepo(string repoId)
    {
        var vpmPackageEntities = await repoMetaDataService.GetVpmPackages(repoId);

        var packages =
            vpmPackageEntities
                .GroupBy(package => package.Name)
                .Select(package =>
                    package.Select(version =>
                            new KeyValuePair<string, VpmPackage>(version.Version, GetPackageWithUrl(version)))
                        .ToDictionary())
                .Select(packageVersions => new KeyValuePair<string, VpmRepoPackageVersions>(
                    packageVersions.First().Value.Name,
                    new VpmRepoPackageVersions(packageVersions
                        .OrderByDescending(package => package.Value.Version, SemVersion.SortOrderComparer)
                        .ToDictionary())))
                .ToDictionary();

        var repo = new VpmRepo($"{repoId}@{options.Value.RepoName}", options.Value.RepoAuthor,
            options.Value.RepoUrl.Replace("{id}", repoId),
            $"{options.Value.RepoId}.{repoId}",
            packages);

        return new JsonResult(repo, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private VpmPackage GetPackageWithUrl(VpmPackageEntity package)
    {
        var vpmPackage = mapper.Map<VpmPackage>(package);

        var fileDownloadEndpoint = new Uri(fileHostOptions.Value.BaseUrl,
            $"files/{package.FileId}/download/{package.UpstreamId}@{package.PackageId}.zip").ToString();
        vpmPackage.Url = fileDownloadEndpoint;

        return vpmPackage;
    }
}