using System.ComponentModel.DataAnnotations;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using VPMReposSynchronizer.Core.Models.Types;
using VPMReposSynchronizer.Core.Services.RepoSync;

namespace VPMReposSynchronizer.Entry.Controllers;

[ApiController]
[Route("syncTasks")]
[Produces("application/json")]
public class SyncTaskController(RepoSyncTaskService repoSyncTaskService, IMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<SyncTaskPublic[]> Index([Range(0, int.MaxValue)] int offset = 0, [Range(1, 100)] int limit = 20)
    {
        var syncTasks = await repoSyncTaskService.GetSyncTasksAsync(offset, limit);

        return mapper.Map<SyncTaskPublic[]>(syncTasks);
    }

    [HttpGet]
    [Route("{id:long}")]
    [ProducesResponseType<SyncTaskPublic>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSyncTask(long id)
    {
        var syncTask = await repoSyncTaskService.GetSyncTaskAsync(id);

        if (syncTask is null)
        {
            return NotFound();
        }

        return Ok(mapper.Map<SyncTaskPublic>(syncTask));
    }

    [HttpGet]
    [Route("{id:long}/logs")]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadLogs(long id)
    {
        var syncTask = await repoSyncTaskService.GetSyncTaskAsync(id);

        if (syncTask is null)
        {
            return NotFound();
        }

        var logPath = syncTask.LogPath;

        if (!System.IO.File.Exists(logPath))
        {
            return NotFound();
        }

        return PhysicalFile(logPath, "text/plain");
    }
}
