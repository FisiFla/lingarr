using Lingarr.Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lingarr.Server.Controllers;

/// <summary>
/// Health check controller. Uses [AllowAnonymous] intentionally — health checks
/// must be accessible without authentication for Docker HEALTHCHECK and orchestrators.
/// Route is /api/health (matching the HEALTHCHECK CMD in the Dockerfile).
/// </summary>
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
    /// Verifies the application can connect to the database within 5 seconds.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> Health()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _dbContext.Database.CanConnectAsync(cts.Token);
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }
        catch
        {
            return StatusCode(503, new { status = "unhealthy", timestamp = DateTime.UtcNow });
        }
    }
}
