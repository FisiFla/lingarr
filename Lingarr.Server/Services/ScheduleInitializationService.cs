using Lingarr.Server.Interfaces.Services;

namespace Lingarr.Server.Services;

public class ScheduleInitializationService : IHostedService
{
    private readonly IScheduleService _scheduleService;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<ScheduleInitializationService> _logger;

    public ScheduleInitializationService(
        IScheduleService scheduleService,
        IHostApplicationLifetime appLifetime,
        ILogger<ScheduleInitializationService> logger)
    {
        _scheduleService = scheduleService;
        _appLifetime = appLifetime;
        _logger = logger;
    }
    
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _appLifetime.ApplicationStarted.Register(() =>
        {
            // Fire-and-forget with proper exception handling.
            // ApplicationStarted.Register only accepts Action, so async void is unavoidable here,
            // but we ensure exceptions are always caught and logged.
            _ = InitializeAsync();
        });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Performs the initialization of the <see cref="_scheduleService"/> when the application starts.
    /// </summary>
    private async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing Schedule Service...");
            await _scheduleService.Initialize();
            _logger.LogInformation("Schedule Service initialized successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while initializing ScheduleService.");
        }
    }
}