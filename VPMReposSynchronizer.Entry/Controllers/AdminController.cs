using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using VPMReposSynchronizer.Core.Services;

namespace VPMReposSynchronizer.Entry.Controllers;

[ApiController]
[Route("admin")]
public class AdminController(RepoSyncTaskService repoSyncTaskService, IMapper mapper) : ControllerBase
{
}