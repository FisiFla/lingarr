using Lingarr.Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lingarr.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly LingarrDbContext _dbContext;

    public HealthController(LingarrDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Health check endpoint for container orchestration and monitoring.
    /// Verifies the application can connect to the database.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> Health()
    {
        try
        {
            await _dbContext.Database.CanConnectAsync();
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }
        catch
        {
            return StatusCode(503, new { status = "unhealthy", timestamp = DateTime.UtcNow });
        }
    }
}
