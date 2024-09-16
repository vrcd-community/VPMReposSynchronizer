using System.ComponentModel.DataAnnotations;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VPMReposSynchronizer.Core.Models.Types;
using VPMReposSynchronizer.Core.Services;
using VPMReposSynchronizer.Core.Services.RepoSync;

namespace VPMReposSynchronizer.Entry.Controllers;

[ApiController]
[Route("syncTasks")]
[Produces("application/json")]
public class SyncTaskController(
    RepoSyncTaskService repoSyncTaskService,
    RepoMetaDataService repoMetaDataService,
    RepoSyncTaskScheduleService repoSyncTaskScheduleService,
    IMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<PageResult<SyncTaskPublic>> Index([Range(0, int.MaxValue)] int page = 0, [Range(1, 100)] int count = 20, string? repoId = null)
    {
        var syncTasks = await repoSyncTaskService.GetSyncTasksAsync(page, count, repoId);

        var tasks = mapper.Map<SyncTaskPublic[]>(syncTasks.Items);

        return new PageResult<SyncTaskPublic>(tasks, syncTasks.TotalCount);
    }

    [HttpPost("{repoId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(AuthenticationSchemes = "ApiKey", Policy = "ApiKey")]
    public async Task<IActionResult> CreateSyncTask(string repoId)
    {
        if (!await repoMetaDataService.IsRepoExist(repoId)) return NotFound();

        await repoSyncTaskScheduleService.InvokeSyncTaskAsync(repoId);
        return NoContent();
    }

    [HttpGet]
    [Route("{id:long}")]
    [ProducesResponseType<SyncTaskPublic>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSyncTask(long id)
    {
        var syncTask = await repoSyncTaskService.GetSyncTaskAsync(id);

        if (syncTask is null) return NotFound();

        return Ok(mapper.Map<SyncTaskPublic>(syncTask));
    }

    [HttpGet]
    [Route("{id:long}/logs")]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadLogs(long id)
    {
        var syncTask = await repoSyncTaskService.GetSyncTaskAsync(id);

        if (syncTask is null) return NotFound();

        var logPath = Path.GetFullPath(syncTask.LogPath);

        if (!System.IO.File.Exists(logPath)) return NotFound();

        return PhysicalFile(logPath, "text/plain");
    }
}
