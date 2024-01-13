using Microsoft.AspNetCore.Mvc;
using VPMReposSynchronizer.Core.Models.Types;
using VPMReposSynchronizer.Core.Services;

namespace VPMReposSynchronizer.Entry.Controllers;

/// <summary>
/// Service Status Controller
/// </summary>
[ApiController]
[Route("status")]
[Produces("application/json")]
public class StatusController(RepoSynchronizerStatusService repoSynchronizerStatusService) : ControllerBase
{
    /// <summary>
    /// Get sync status.
    /// </summary>
    /// <returns>Sync Status</returns>
    /// <remarks>
    /// Sample Response:
    ///
    ///     [
    ///         {
    ///             "syncStarted": "2024-01-13T23:15:09.622317+08:00",
    ///             "syncEnded": "2024-01-13T23:15:11.9102018+08:00",
    ///             "status": 0,
    ///             "id": "curated",
    ///             "url": "https://packages.vrchat.com/curated",
    ///             "message": ""
    ///         }
    ///     ]
    ///
    /// </remarks>
    /// <response code="200">Sync Status</response>
    [Route("sync")]
    [HttpGet]
    [ProducesResponseType<SyncStatusPublic[]>(StatusCodes.Status200OK)]
    public SyncStatusPublic[] SyncStatus()
    {
        return repoSynchronizerStatusService.SyncStatus
            .Select(status => new SyncStatusPublic(status.Key, status.Value))
            .ToArray();
    }
}