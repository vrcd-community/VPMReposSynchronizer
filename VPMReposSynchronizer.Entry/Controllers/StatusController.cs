using Microsoft.AspNetCore.Mvc;
using VPMReposSynchronizer.Core.Models.Types;
using VPMReposSynchronizer.Core.Services;

namespace VPMReposSynchronizer.Entry.Controllers;

[ApiController]
[Route("status")]
[Produces("application/json")]
public class StatusController(RepoSynchronizerStatusService repoSynchronizerStatusService) : ControllerBase
{
    [Route("sync")]
    [HttpGet]
    public SyncStatusPublic[] SyncStatus()
    {
        return repoSynchronizerStatusService.SyncStatus
            .Select(status => new SyncStatusPublic(status.Key, status.Value))
            .ToArray();
    }
}