namespace FamilyNido.Api.Features.Notifications;

/// <summary>
/// Drains the <see cref="EmailDispatchService"/> queue and hands each message
/// to the resolved <see cref="IEmailSender"/>. Each send runs in its own DI
/// scope so transient services (logger, options snapshots) line up correctly,
/// and exceptions in one send never block the next.
/// </summary>
public sealed class EmailDispatchBackgroundService : BackgroundService
{
    private readonly EmailDispatchService _dispatcher;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailDispatchBackgroundService> _logger;

    /// <summary>Primary constructor.</summary>
    public EmailDispatchBackgroundService(
        EmailDispatchService dispatcher,
        IServiceScopeFactory scopeFactory,
        ILogger<EmailDispatchBackgroundService> logger)
    {
        _dispatcher = dispatcher;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in _dispatcher.Reader.ReadAllAsync(stoppingToken))
            {
                await SendOneAsync(message, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown — drop pending messages on the floor by design.
        }
    }

    private async Task SendOneAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var result = await sender.SendAsync(message, cancellationToken);
            if (!result.Delivered)
            {
                _logger.LogWarning(
                    "Email to {To} not delivered: {Reason}",
                    message.To,
                    result.ErrorReason ?? "unknown");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background email dispatch failed for {To}", message.To);
        }
    }
}
