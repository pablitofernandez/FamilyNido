using FamilyNido.Api.Options;
using Microsoft.Extensions.Options;

namespace FamilyNido.Api.Features.Calendar;

/// <summary>
/// Hosted service that runs <see cref="CalendarSynchronizer.SyncAllAsync"/> on a
/// fixed cadence (<see cref="CalendarOptions.SyncInterval"/>). Sleeps with a
/// <see cref="PeriodicTimer"/> so the loop wakes up promptly on shutdown.
/// </summary>
public sealed class CalendarSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<CalendarOptions> _options;
    private readonly ILogger<CalendarSyncBackgroundService> _logger;

    /// <summary>Primary constructor.</summary>
    public CalendarSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<CalendarOptions> options,
        ILogger<CalendarSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var initialDelay = TimeSpan.FromSeconds(30);
        try
        {
            // Don't pile work onto a cold-starting app. A short head-start means the API is
            // already serving traffic when the first sync runs.
            await Task.Delay(initialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await SyncOnceAsync(stoppingToken);

            var interval = _options.CurrentValue.SyncInterval;
            if (interval <= TimeSpan.Zero)
            {
                interval = TimeSpan.FromMinutes(15);
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task SyncOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var synchronizer = scope.ServiceProvider.GetRequiredService<CalendarSynchronizer>();
            await synchronizer.SyncAllAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Shutting down; do not log.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Calendar sync iteration failed at the top level.");
        }
    }
}
