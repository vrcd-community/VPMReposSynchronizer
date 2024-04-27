using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VPMReposSynchronizer.Core.Services.FileHost;

namespace VPMReposSynchronizer.Entry.Controllers;

[ApiController]
[Route("files")]
[Produces("application/json")]
public class FileController(IFileHostService fileHostService) : ControllerBase
{
    /// <summary>
    /// Redirect to file uri.
    /// </summary>
    /// <param name="fileId">File id</param>
    /// <returns>Redirect to file uri.</returns>
    /// <response code="302">File uri</response>
    /// <response code="404">Request file doesn't exist</response>
    /// <response code="429">Send too many request in a time</response>
    /// <response code="403">You have been banned</response>
    [Route("{fileId}/download/{fileName}")]
    [Route("{fileId}/download")]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [EnableRateLimiting("download")]
    public async Task<IActionResult> DownloadFile(string fileId)
    {
        if (!await fileHostService.IsFileExist(fileId))
        {
            return NotFound();
        }

        var fileUri = await fileHostService.GetFileUriAsync(fileId);

        return Redirect(fileUri);
    }
}