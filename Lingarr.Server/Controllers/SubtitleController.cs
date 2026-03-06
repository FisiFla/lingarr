using Lingarr.Server.Attributes;
using Lingarr.Server.Interfaces.Services;
using Lingarr.Server.Models;
using Lingarr.Server.Models.FileSystem;
using Lingarr.Server.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Lingarr.Server.Controllers;

[ApiController]
[LingarrAuthorize]
[Route("api/[controller]")]
public class SubtitleController : ControllerBase
{
    private readonly ISubtitleService _subtitleService;
    private static readonly string[] AllowedRoots = (Environment.GetEnvironmentVariable("ALLOWED_MEDIA_PATHS") ?? "/media,/movies,/tv")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public SubtitleController(ISubtitleService subtitleService)
    {
        _subtitleService = subtitleService;
    }
    
    /// <summary>
    /// Retrieves a list of subtitle files located at the specified path.
    /// </summary>
    /// <param name="subtitlePath">The directory path to search for subtitle files.This path is relative to the media folder
    /// and should not start with a forward slash.</param>
    /// <returns>Returns an HTTP 200 OK response with a list of <see cref="Subtitles"/> objects found at the specified path.</returns>
    [HttpPost("all")]
    public async Task<ActionResult<List<Subtitles>>> GetAllSubtitles([FromBody] SubtitlePath subtitlePath)
    {
        var resolvedPath = Path.GetFullPath(subtitlePath.Path);
        if (!PathSecurity.IsPathUnderAnyRoot(resolvedPath, AllowedRoots))
        {
            return BadRequest("Path is outside allowed media directories.");
        }

        var value = await _subtitleService.GetAllSubtitles(resolvedPath);
        return Ok(value);
    }
}
