namespace FamilyNido.Api.Options;

/// <summary>
/// Outbound email configuration. FamilyNido never accepts mail; this section
/// only describes how the API talks to a generic SMTP relay (Brevo, Gmail
/// app-password, mailcow, an internal MTA…). Disabled by default so the
/// system stays usable without an email infrastructure: when
/// <see cref="Enabled"/> is false the sender becomes a no-op and admins
/// fall back to copying invitation links manually.
/// </summary>
public sealed class EmailOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Email";

    /// <summary>
    /// Master switch. When false the API registers <c>NullEmailSender</c> and
    /// every send call returns <c>Delivered=false</c>. Useful for offline dev
    /// and for emergencies in prod.
    /// </summary>
    public bool Enabled { get; init; } = false;

    /// <summary>SMTP host name. Required when <see cref="Enabled"/> is true.</summary>
    public string SmtpHost { get; init; } = "";

    /// <summary>SMTP port. 587 for STARTTLS submission, 465 for implicit TLS, 25 for plain.</summary>
    public int SmtpPort { get; init; } = 587;

    /// <summary>Username for SMTP AUTH. Empty disables auth (open relay or trusted network).</summary>
    public string SmtpUsername { get; init; } = "";

    /// <summary>Password / app password for SMTP AUTH. Stored as plain config — keep out of source control.</summary>
    public string SmtpPassword { get; init; } = "";

    /// <summary>
    /// When true the connection is opened plain and upgraded to TLS via
    /// <c>STARTTLS</c> (the standard for port 587). When false the client
    /// connects and treats the port's TLS posture according to MailKit's
    /// auto-detection (implicit TLS for 465, plain for 25).
    /// </summary>
    public bool SmtpUseStartTls { get; init; } = true;

    /// <summary>
    /// "From" header used on outgoing messages. RFC 5322 mailbox syntax,
    /// e.g. <c>FamilyNido &lt;noreply@example.com&gt;</c>.
    /// </summary>
    public string From { get; init; } = "FamilyNido <noreply@example.com>";

    /// <summary>
    /// Public origin used to build invitation and email links. No trailing
    /// slash. E.g. <c>https://familia.example.com</c> in production.
    /// Operators MUST set this to the front-facing host; the default points
    /// at the local Angular dev server so a fresh checkout doesn't hand out
    /// dead links, but it's still wrong for prod / CI / any other env.
    /// </summary>
    public string AppBaseUrl { get; init; } = "http://localhost:4200";

    /// <summary>How long an invitation token remains redeemable. Default 7 days.</summary>
    public TimeSpan InvitationLifetime { get; init; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Hour-of-day (0..23) in each family's local timezone at which the daily
    /// digest is allowed to fire. The background scheduler wakes every few
    /// minutes and only emails when the local clock has crossed this threshold
    /// for the first time on a given day. Default 7 (= 7:00 local).
    /// </summary>
    public int DigestHour { get; init; } = 7;
}
