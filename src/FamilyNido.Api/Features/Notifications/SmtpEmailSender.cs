using FamilyNido.Api.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FamilyNido.Api.Features.Notifications;

/// <summary>
/// MailKit-backed SMTP sender. Talks to a generic relay configured by
/// <see cref="EmailOptions"/>: Brevo, Gmail with an app password, mailcow,
/// a corporate MTA, etc.
/// </summary>
/// <remarks>
/// Connection posture is decided by <see cref="EmailOptions.SmtpUseStartTls"/>:
/// <list type="bullet">
///   <item>true → STARTTLS (port 587 submission). The connection opens plain and is upgraded.</item>
///   <item>false → MailKit's <c>Auto</c>: implicit TLS on 465, plain on 25.</item>
/// </list>
/// Authentication is skipped when <see cref="EmailOptions.SmtpUsername"/> is
/// empty (useful for trusted-network relays). All transport failures are
/// caught and converted into <see cref="EmailResult"/> with
/// <c>Delivered=false</c>; callers never see exceptions from this layer.
/// </remarks>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IOptionsMonitor<EmailOptions> _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    /// <summary>Primary constructor.</summary>
    public SmtpEmailSender(IOptionsMonitor<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<EmailResult> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = _options.CurrentValue;

        if (string.IsNullOrWhiteSpace(settings.SmtpHost))
        {
            _logger.LogWarning("SMTP host not configured; cannot send {Subject} to {To}", message.Subject, message.To);
            return new EmailResult(false, null, "smtp_host_missing");
        }

        var mime = BuildMimeMessage(settings, message);

        try
        {
            using var client = new SmtpClient();

            var secureSocket = settings.SmtpUseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.Auto;

            await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, secureSocket, cancellationToken);

            if (!string.IsNullOrEmpty(settings.SmtpUsername))
            {
                await client.AuthenticateAsync(settings.SmtpUsername, settings.SmtpPassword, cancellationToken);
            }

            // MailKit returns the server's response string; we just preserve a
            // truncated form as ProviderMessageId for traceability.
            var serverResponse = await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);

            return new EmailResult(true, Truncate(serverResponse, 120), null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "SMTP send failed for {To} via {Host}:{Port}",
                message.To,
                settings.SmtpHost,
                settings.SmtpPort);

            return new EmailResult(false, null, ClassifyError(ex));
        }
    }

    private static MimeMessage BuildMimeMessage(EmailOptions settings, EmailMessage message)
    {
        var mime = new MimeMessage();

        if (MailboxAddress.TryParse(settings.From, out var from))
        {
            mime.From.Add(from);
        }
        else
        {
            // Fall back to a structured address so we never produce an unparseable
            // From header — better to ship "noreply" than to fail the whole send.
            mime.From.Add(new MailboxAddress("FamilyNido", settings.From));
        }

        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;

        // HTML-only body. Modern mail clients render it; text-only clients
        // see a tags-stripped fallback that MailKit produces from the part
        // structure. We avoid building a hand-rolled plain alternative until
        // we have a real template — the invitation email is short enough that
        // even tag-stripped renders look fine.
        var builder = new BodyBuilder { HtmlBody = message.HtmlBody };
        mime.Body = builder.ToMessageBody();

        return mime;
    }

    private static string ClassifyError(Exception ex) => ex switch
    {
        AuthenticationException => "smtp_auth_failed",
        SslHandshakeException => "smtp_tls_failed",
        SmtpCommandException smtpEx => $"smtp_command_{(int)smtpEx.StatusCode}",
        SmtpProtocolException => "smtp_protocol",
        _ => "smtp_unknown",
    };

    private static string Truncate(string? value, int max)
        => string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Length <= max ? value : value[..max];
}
