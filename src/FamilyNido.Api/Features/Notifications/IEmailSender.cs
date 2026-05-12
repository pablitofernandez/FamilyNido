namespace FamilyNido.Api.Features.Notifications;

/// <summary>
/// Abstraction over outbound email delivery. Implementations never throw on
/// transport failures: they return a structured <see cref="EmailResult"/> so
/// callers can decide whether to retry, fall back to "copy link", or log.
/// </summary>
public interface IEmailSender
{
    /// <summary>Sends one message. Always returns; never throws.</summary>
    Task<EmailResult> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>One outbound email payload.</summary>
/// <param name="To">RFC 5322 mailbox of the recipient.</param>
/// <param name="Subject">Plain-text subject line.</param>
/// <param name="HtmlBody">Pre-rendered HTML body. The sender attaches a plain-text alternative automatically.</param>
public sealed record EmailMessage(string To, string Subject, string HtmlBody);

/// <summary>Outcome of a send attempt. Always returned, never thrown.</summary>
/// <param name="Delivered">True when the SMTP server accepted the message for delivery.</param>
/// <param name="ProviderMessageId">Optional message id reported by the SMTP server (when available).</param>
/// <param name="ErrorReason">Short machine-readable code when <see cref="Delivered"/> is false.</param>
public sealed record EmailResult(bool Delivered, string? ProviderMessageId, string? ErrorReason);
