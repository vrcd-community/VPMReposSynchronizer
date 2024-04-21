using Microsoft.AspNetCore.Mvc;
using VPMReposSynchronizer.Core.Models.Types;
using VPMReposSynchronizer.Core.Models.Types.RepoBrowser;
using VPMReposSynchronizer.Core.Services;

namespace VPMReposSynchronizer.Entry.Controllers;

[ApiController]
[Route("browser")]
[Produces("application/json")]
public class RepoBrowserController(RepoBrowserService repoBrowserService) : ControllerBase
{
    [Route("repos")]
    [HttpGet]
    [ProducesResponseType<BrowserRepo[]>(StatusCodes.Status200OK)]
    public async Task<BrowserRepo[]> GetAllRepos()
    {
        return await repoBrowserService.GetAllReposAsync();
    }

    [Route("repos/{id}")]
    [HttpGet]
    [ProducesResponseType<BrowserRepo>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRepo(string id)
    {
        var repo = await repoBrowserService.GetRepoAsync(id);

        if (repo is null)
        {
            return NotFound();
        }

        return Ok(repo);
    }

    [Route("repos/{repoId}/packages")]
    [HttpGet]
    [ProducesResponseType<BrowserPackage[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAllPackage(string repoId)
    {
        if (await repoBrowserService.GetRepoAsync(repoId) is null)
        {
            return NotFound();
        }

        var packages = await repoBrowserService.GetAllPackagesAsync(repoId);

        return Ok(packages);
    }

    [Route("repos/{repoId}/packages/{packageId}")]
    [HttpGet]
    [ProducesResponseType<BrowserPackage>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPackage(string repoId, string packageId)
    {
        var package = await repoBrowserService.GetPackageAsync(repoId, packageId);
        if (package == null)
        {
            return NotFound();
        }

        return Ok(package);
    }

    [HttpGet]
    [Route("packages/search")]
    [ProducesResponseType<BrowserPackage[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<BrowserPackage[]> SearchPackage(string keyword)
    {
        var packages = await repoBrowserService.SearchVpmPackages(keyword);

        return packages;
    }
}