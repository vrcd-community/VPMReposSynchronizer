using Microsoft.AspNetCore.Mvc;
using VPMReposSynchronizer.Core.Models.Types;
using VPMReposSynchronizer.Core.Services;
using VPMReposSynchronizer.Core.Utils;

namespace VPMReposSynchronizer.Entry.Controllers;

/// <summary>
/// Service Status Controller
/// </summary>
[ApiController]
[Route("status")]
[Produces("application/json")]
public class StatusController(RepoSyncTaskService repoSyncTaskService) : ControllerBase
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
        return [];
    }

    /// <summary>
    /// Get build information.
    /// </summary>
    /// <remarks>
    /// Sample Response:
    /// {
    ///     "version": "0.10.0.0",
    ///     "architecture": "X64",
    ///     "buildDate": "2024-04-03T14:22:43.0355807+00:00",
    ///     "commitHash": "3ca66743c2e575bddee435a1ca6066ce98e58ec2",
    ///     "branchName": "main"
    /// }
    /// </remarks>
    /// <returns>Build Info</returns>
    [Route("buildInfo")]
    [HttpGet]
    [ProducesResponseType<BuildInfo>(StatusCodes.Status200OK)]
    public BuildInfo GetSynchronizerBuildInformation()
    {
        return BuildInfoUtils.GetBuildInfo();
    }
}