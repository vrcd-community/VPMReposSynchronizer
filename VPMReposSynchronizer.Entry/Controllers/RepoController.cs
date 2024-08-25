using AutoMapper;
using Cronos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Models.Types.RepoAdmin;
using VPMReposSynchronizer.Core.Models.Types.RepoBrowser;
using VPMReposSynchronizer.Core.Services;

namespace VPMReposSynchronizer.Entry.Controllers;

[ApiController]
[Route("repos")]
[Produces("application/json")]
public class RepoController(
    RepoBrowserService repoBrowserService,
    RepoMetaDataService repoMetaDataService,
    IMapper mapper) : ControllerBase
{
    [Route("")]
    [HttpGet]
    [ProducesResponseType<BrowserRepo[]>(StatusCodes.Status200OK)]
    public async Task<BrowserRepo[]> GetAllRepos()
    {
        return await repoBrowserService.GetAllReposAsync();
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Authorize(AuthenticationSchemes = "ApiKey", Policy = "ApiKey")]
    public async Task<IActionResult> Create(RepoAdminUpdateDto repo)
    {
        var repoEntity = mapper.Map<VpmRepoEntity>(repo);

        if (!Uri.TryCreate(repoEntity.UpStreamUrl, UriKind.Absolute, out _)) return BadRequest("Invalid url.");

        if (!CronExpression.TryParse(repoEntity.SyncTaskCron, out _)) return BadRequest("Invalid cron expression.");

        await repoMetaDataService.AddRepoAsync(repoEntity);
        return NoContent();
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Authorize(AuthenticationSchemes = "ApiKey", Policy = "ApiKey")]
    public async Task<IActionResult> Update(string id, RepoAdminUpdateDto repo)
    {
        if (!await repoMetaDataService.IsRepoExist(id)) return NotFound();

        var repoEntity = mapper.Map<VpmRepoEntity>(repo);
        repoEntity.Id = id;

        if (!Uri.TryCreate(repoEntity.UpStreamUrl, UriKind.Absolute, out _)) return BadRequest("Invalid url.");

        if (!CronExpression.TryParse(repoEntity.SyncTaskCron, out _)) return BadRequest("Invalid cron expression.");

        await repoMetaDataService.UpdateRepoAsync(repoEntity);

        return NoContent();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(AuthenticationSchemes = "ApiKey", Policy = "ApiKey")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await repoMetaDataService.IsRepoExist(id)) return NotFound();

        await repoMetaDataService.DeleteRepoAsync(id);

        return NoContent();
    }

    [Route("{id}")]
    [HttpGet]
    [ProducesResponseType<BrowserRepo>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRepo(string id)
    {
        var repo = await repoBrowserService.GetRepoAsync(id);

        if (repo is null) return NotFound();

        return Ok(repo);
    }

    [Route("{repoId}/packages")]
    [HttpGet]
    [ProducesResponseType<BrowserPackage[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAllPackage(string repoId)
    {
        if (await repoBrowserService.GetRepoAsync(repoId) is null) return NotFound();

        var packages = await repoBrowserService.GetAllPackagesAsync(repoId);

        return Ok(packages);
    }

    [Route("{repoId}/packages/{packageId}")]
    [HttpGet]
    [ProducesResponseType<BrowserPackage>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPackage(string repoId, string packageId)
    {
        var package = await repoBrowserService.GetPackageAsync(repoId, packageId);
        if (package == null) return NotFound();

        return Ok(package);
    }
}
