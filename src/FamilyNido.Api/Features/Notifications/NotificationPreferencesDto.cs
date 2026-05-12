namespace FamilyNido.Api.Features.Notifications;

/// <summary>Wire shape returned by GET/PUT /api/notifications/preferences.</summary>
/// <param name="EmailEnabled">Master switch — disables every email regardless of the others.</param>
/// <param name="DigestEnabled">Daily morning digest with today's tasks/events/birthdays.</param>
/// <param name="TaskAssignedEnabled">Email when assigned as the responsible of a new or edited task.</param>
/// <param name="WallMentionEnabled">Email when mentioned via <c>@</c> on the wall.</param>
public sealed record NotificationPreferencesDto(
    bool EmailEnabled,
    bool DigestEnabled,
    bool TaskAssignedEnabled,
    bool WallMentionEnabled);
