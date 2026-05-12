using FamilyNido.Api.Options;
using FamilyNido.Domain.HouseholdTasks;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FamilyNido.Api.Features.Notifications;

/// <summary>
/// Translates domain events ("a task got a new responsible", "a member was
/// mentioned on the wall") into outbound email payloads and queues them
/// through <see cref="EmailDispatchService"/>. Centralising the lookup here
/// keeps the slices ignorant of preferences, templates and SMTP plumbing.
/// </summary>
/// <remarks>
/// The service is scoped (it owns an <see cref="ApplicationDbContext"/>) and
/// is meant to be injected into request handlers. All methods return when the
/// message is enqueued — actual delivery is handled asynchronously by
/// <see cref="EmailDispatchBackgroundService"/>.
/// </remarks>
public sealed class NotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly EmailDispatchService _dispatcher;
    private readonly IOptionsMonitor<EmailOptions> _emailOptions;
    private readonly ILogger<NotificationService> _logger;

    /// <summary>Primary constructor.</summary>
    public NotificationService(
        ApplicationDbContext db,
        EmailDispatchService dispatcher,
        IOptionsMonitor<EmailOptions> emailOptions,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _dispatcher = dispatcher;
        _emailOptions = emailOptions;
        _logger = logger;
    }

    /// <summary>
    /// Notify the responsible of a task. Skips when actor==recipient (you don't
    /// email yourself), when the recipient is not linked to a user, when the
    /// user has no email configured, or when their preferences have either
    /// the master switch or the per-type toggle disabled.
    /// </summary>
    /// <param name="responsibleMemberId">Member newly assigned as responsible.</param>
    /// <param name="actorMemberId">Member that performed the assignment (the caller).</param>
    /// <param name="task">Task aggregate, used for body content.</param>
    /// <param name="cancellationToken">Cancellation token from the request scope.</param>
    public async Task NotifyTaskAssignedAsync(
        Guid responsibleMemberId,
        Guid actorMemberId,
        HouseholdTask task,
        CancellationToken cancellationToken)
    {
        if (responsibleMemberId == actorMemberId)
        {
            return;
        }

        var recipient = await _db.FamilyMembers
            .Where(m => m.Id == responsibleMemberId)
            .Select(m => new
            {
                MemberDisplayName = m.DisplayName,
                Email = m.User != null ? m.User.Email : null,
                UserId = m.UserId,
                Language = m.User != null ? m.User.PreferredLanguage : "es-ES",
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (recipient is null || string.IsNullOrEmpty(recipient.Email) || recipient.UserId is null)
        {
            return;
        }

        if (!await IsAllowedAsync(recipient.UserId.Value, p => p.TaskAssignedEnabled, cancellationToken))
        {
            return;
        }

        var actorName = await ResolveDisplayNameAsync(actorMemberId, cancellationToken);
        var (subject, html) = EmailTemplates.TaskAssigned(
            actorName,
            recipient.MemberDisplayName,
            task,
            _emailOptions.CurrentValue.AppBaseUrl,
            recipient.Language);

        _dispatcher.Queue(new EmailMessage(recipient.Email, subject, html));
    }

    /// <summary>
    /// Notify a set of mentioned members. The actor is filtered out (no
    /// self-notify) and each remaining recipient is checked individually
    /// against their preferences.
    /// </summary>
    /// <param name="mentionedMemberIds">Distinct member ids extracted from the post.</param>
    /// <param name="actorMemberId">Author of the message/comment.</param>
    /// <param name="contextKind">Either <c>"message"</c> or <c>"comment"</c>; the human label is resolved per recipient locale.</param>
    /// <param name="snippet">Plain-text snippet of the source for the email body.</param>
    /// <param name="cancellationToken">Cancellation token from the request scope.</param>
    public async Task NotifyWallMentionAsync(
        IReadOnlyList<Guid> mentionedMemberIds,
        Guid actorMemberId,
        string contextKind,
        string snippet,
        CancellationToken cancellationToken)
    {
        var targets = mentionedMemberIds.Where(id => id != actorMemberId).Distinct().ToHashSet();
        if (targets.Count == 0)
        {
            return;
        }

        var recipients = await _db.FamilyMembers
            .Where(m => targets.Contains(m.Id))
            .Select(m => new
            {
                MemberDisplayName = m.DisplayName,
                Email = m.User != null ? m.User.Email : null,
                UserId = m.UserId,
                Language = m.User != null ? m.User.PreferredLanguage : "es-ES",
            })
            .ToListAsync(cancellationToken);

        var userIds = recipients
            .Where(r => r.UserId is not null)
            .Select(r => r.UserId!.Value)
            .ToList();

        var prefs = new Dictionary<Guid, UserNotificationToggles>();
        if (userIds.Count > 0)
        {
            var rows = await _db.UserNotificationPreferences
                .AsNoTracking()
                .Where(p => userIds.Contains(p.UserId))
                .Select(p => new { p.UserId, p.EmailEnabled, p.WallMentionEnabled })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                prefs[row.UserId] = new UserNotificationToggles(row.EmailEnabled, row.WallMentionEnabled);
            }
        }

        var actorName = await ResolveDisplayNameAsync(actorMemberId, cancellationToken);
        var baseUrl = _emailOptions.CurrentValue.AppBaseUrl;

        foreach (var r in recipients)
        {
            if (string.IsNullOrEmpty(r.Email) || r.UserId is null)
            {
                continue;
            }

            // Default to enabled when there is no row yet — a brand-new user
            // gets useful notifications until they explicitly turn them off.
            var toggles = prefs.TryGetValue(r.UserId.Value, out var t)
                ? t
                : new UserNotificationToggles(EmailEnabled: true, ChannelEnabled: true);

            if (!toggles.EmailEnabled || !toggles.ChannelEnabled)
            {
                continue;
            }

            var contextLabel = EmailTemplates.WallMentionContextLabel(contextKind, r.Language);
            var (subject, html) = EmailTemplates.WallMention(
                actorName,
                r.MemberDisplayName,
                contextLabel,
                snippet,
                baseUrl,
                r.Language);

            _dispatcher.Queue(new EmailMessage(r.Email, subject, html));
        }
    }

    private async Task<bool> IsAllowedAsync(
        Guid userId,
        Func<Domain.Notifications.UserNotificationPreferences, bool> channel,
        CancellationToken cancellationToken)
    {
        var prefs = await _db.UserNotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        // Defaults: everything on. Master switch wins.
        if (prefs is null)
        {
            return true;
        }

        return prefs.EmailEnabled && channel(prefs);
    }

    private async Task<string> ResolveDisplayNameAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var name = await _db.FamilyMembers
            .Where(m => m.Id == memberId)
            .Select(m => m.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);

        // Fallback used inside email greetings when the actor row was deleted
        // mid-flight. Stays in Spanish — the recipient locale isn't known here
        // and the email template will still localize the surrounding sentence.
        return string.IsNullOrEmpty(name) ? "Alguien de la familia" : name;
    }

    /// <summary>Compact prefs view to avoid loading the whole entity per recipient.</summary>
    private readonly record struct UserNotificationToggles(bool EmailEnabled, bool ChannelEnabled);
}
