using Microsoft.AspNetCore.Mvc;
using VPMReposSynchronizer.Core.Models.Types.RepoBrowser;
using VPMReposSynchronizer.Core.Services;

namespace VPMReposSynchronizer.Entry.Controllers;

[ApiController]
[Route("packages")]
[Produces("application/json")]
public class PackageController(RepoBrowserService repoBrowserService) : ControllerBase
{
    [HttpGet]
    [Route("search")]
    [ProducesResponseType<BrowserPackage[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<BrowserPackage[]> SearchPackage(string keyword)
    {
        var packages = await repoBrowserService.SearchVpmPackages(keyword);

        return packages;
    }
}