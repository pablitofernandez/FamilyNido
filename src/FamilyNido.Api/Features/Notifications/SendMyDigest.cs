using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Options;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.Extensions.Options;

namespace FamilyNido.Api.Features.Notifications;

/// <summary>
/// Slice for <c>POST /api/notifications/digest/me</c>. Sends a "today" digest
/// email to the calling user only, useful for previewing the template
/// without waiting for the morning scheduler. Reuses <see cref="EmailDigestBackgroundService.BuildContentAsync"/>
/// so the manual trigger and the scheduled tick render the same email.
/// </summary>
/// <remarks>
/// Unlike the scheduled run, this slice does <em>not</em> insert an
/// <see cref="Domain.Notifications.EmailDigestRun"/> row. That keeps it
/// idempotent in the "I want to preview again" sense and prevents the
/// scheduled tick from skipping today's real digest because someone hit
/// the preview button. The email goes only to the requesting user, so a
/// preview never spams the rest of the family.
/// </remarks>
public static class SendMyDigest
{
    /// <summary>Empty command — the recipient is whoever is calling.</summary>
    public sealed record Command : IRequest<Result<Response>>;

    /// <summary>Result returned to the front so it can confirm the send.</summary>
    /// <param name="Email">Address the email was queued to.</param>
    /// <param name="IsEmpty">True when the digest had nothing to report (no email queued).</param>
    public sealed record Response(string Email, bool IsEmpty);

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<Response>>
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserContext _userContext;
        private readonly EmailDispatchService _dispatcher;
        private readonly IOptions<EmailOptions> _emailOptions;
        private readonly TimeProvider _timeProvider;

        /// <summary>Primary constructor.</summary>
        public Handler(
            ApplicationDbContext db,
            ICurrentUserContext userContext,
            EmailDispatchService dispatcher,
            IOptions<EmailOptions> emailOptions,
            TimeProvider timeProvider)
        {
            _db = db;
            _userContext = userContext;
            _dispatcher = dispatcher;
            _emailOptions = emailOptions;
            _timeProvider = timeProvider;
        }

        /// <inheritdoc />
        public async Task<Result<Response>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }
            if (string.IsNullOrEmpty(current.User.Email))
            {
                return ApplicationError.Validation("digest.no_email", "Tu cuenta no tiene email asociado.");
            }

            // Resolve the family timezone the same way the scheduler does so the
            // "today" window matches what the morning email would cover.
            TimeZoneInfo tz;
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById(current.Family.TimeZone);
            }
            catch (TimeZoneNotFoundException)
            {
                tz = TimeZoneInfo.Utc;
            }
            var localDate = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), tz).DateTime);

            var recipient = new EmailDigestBackgroundService.RecipientRow(
                MemberId: current.Member.Id,
                DisplayName: current.Member.DisplayName,
                UserId: current.User.Id,
                Email: current.User.Email,
                Language: current.User.PreferredLanguage);

            var content = await EmailDigestBackgroundService.BuildContentAsync(
                _db, current.Family.Id, recipient, localDate, tz, cancellationToken);

            if (content.IsEmpty)
            {
                return new Response(current.User.Email, IsEmpty: true);
            }

            var (subject, html) = EmailTemplates.Digest(
                current.Member.DisplayName, content, _emailOptions.Value.AppBaseUrl, current.User.PreferredLanguage);
            _dispatcher.Queue(new EmailMessage(current.User.Email, $"[Preview] {subject}", html));

            return new Response(current.User.Email, IsEmpty: false);
        }
    }
}
