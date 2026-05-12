using Microsoft.Extensions.Logging;

namespace FamilyNido.Api.Features.Notifications;

/// <summary>
/// Stand-in implementation registered when <c>Email:Enabled=false</c>. Logs
/// the would-be send at information level (not warning — this is a chosen
/// state, not an error) and returns an unsuccessful <see cref="EmailResult"/>
/// so the caller falls back to "copy link" UX.
/// </summary>
public sealed class NullEmailSender : IEmailSender
{
    private readonly ILogger<NullEmailSender> _logger;

    /// <summary>Primary constructor.</summary>
    public NullEmailSender(ILogger<NullEmailSender> logger) => _logger = logger;

    /// <inheritdoc />
    public Task<EmailResult> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Email disabled; would have sent {Subject} to {To}",
            message.Subject,
            message.To);

        return Task.FromResult(new EmailResult(Delivered: false, ProviderMessageId: null, ErrorReason: "email_disabled"));
    }
}
