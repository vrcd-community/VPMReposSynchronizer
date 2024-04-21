using AutoMapper;
using Cronos;
using Microsoft.AspNetCore.Mvc;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Models.Types.RepoAdmin;
using VPMReposSynchronizer.Core.Services;

namespace VPMReposSynchronizer.Entry.Controllers;

[ApiController]
[Route("admin/repos")]
[Produces("application/json")]
public class RepoAdminController(RepoMetaDataService repoMetaDataService, IMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<RepoAdmin[]> Index()
    {
        var repos = await repoMetaDataService.GetAllRepos();

        return mapper.Map<RepoAdmin[]>(repos);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(RepoAdmin repo)
    {
        var repoEntity = mapper.Map<VpmRepoEntity>(repo);

        if (!Uri.TryCreate(repoEntity.UpStreamUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("Invalid url.");
        }

        if (!CronExpression.TryParse(repoEntity.SyncTaskCron, out _))
        {
            throw new InvalidOperationException("Invalid cron expression.");
        }

        await repoMetaDataService.AddRepoAsync(repoEntity);
        return NoContent();
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(string id, RepoAdminUpdateDto repo)
    {
        if (!await repoMetaDataService.IsRepoExist(id))
        {
            return NotFound();
        }

        var repoEntity = mapper.Map<VpmRepoEntity>(repo);
        repoEntity.Id = id;

        if (!Uri.TryCreate(repoEntity.UpStreamUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("Invalid url.");
        }

        if (!CronExpression.TryParse(repoEntity.SyncTaskCron, out _))
        {
            throw new InvalidOperationException("Invalid cron expression.");
        }

        await repoMetaDataService.UpdateRepoAsync(repoEntity);

        return NoContent();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await repoMetaDataService.IsRepoExist(id))
        {
            return NotFound();
        }

        await repoMetaDataService.DeleteRepoAsync(id);

        return NoContent();
    }

    [HttpGet("{id}")]
    [ProducesResponseType<RepoAdmin>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string id)
    {
        var repo = await repoMetaDataService.GetRepoById(id);

        if (repo is null)
        {
            return NotFound();
        }

        return Ok(mapper.Map<RepoAdmin>(repo));
    }

    [HttpPost("{id}/sync")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Sync(string id)
    {
        return NoContent();
    }
}