using System.Net;
using System.Text;
using FamilyNido.Domain.HouseholdTasks;

namespace FamilyNido.Api.Features.Notifications;

/// <summary>
/// HTML email templates rendered as raw interpolated strings — small, no
/// dependency on a templating engine. Inputs are HTML-encoded at the point of
/// substitution; outputs target broad client compatibility (inline styles,
/// no CSS classes, table-free).
/// </summary>
/// <remarks>
/// Every public method takes a <c>lang</c> BCP-47 tag (typically
/// <see cref="Domain.Identity.User.PreferredLanguage"/>) and routes its
/// strings through <see cref="BackendLocalization"/> so the email matches
/// the recipient's chosen frontend bundle.
/// </remarks>
public static class EmailTemplates
{
    /// <summary>Branded subject line for the "task assigned" email.</summary>
    /// <param name="actorName">Display name of the person who assigned the task.</param>
    /// <param name="recipientName">Display name of the responsible (greeted in the body).</param>
    /// <param name="task">Task aggregate, used for title and dates.</param>
    /// <param name="appBaseUrl">Public origin used to build the in-app link.</param>
    /// <param name="lang">BCP-47 language tag of the recipient.</param>
    public static (string Subject, string HtmlBody) TaskAssigned(
        string actorName,
        string recipientName,
        HouseholdTask task,
        string appBaseUrl,
        string lang)
    {
        var T = (string key) => BackendLocalization.T(key, lang);
        var pathPrefix = BackendLocalization.PathPrefix(lang);

        var dueLine = task.DueDate is { } dd
            ? $"<p style=\"margin:6px 0 0;color:#6b6258;font-size:13px;\">{WebUtility.HtmlEncode(T("email.task-assigned.due-prefix") + BackendLocalization.FormatLongDate(dd.ToDateTime(TimeOnly.MinValue), lang))}.</p>"
            : string.Empty;

        var description = string.IsNullOrWhiteSpace(task.Description)
            ? string.Empty
            : $"<p style=\"margin:8px 0 0;color:#3a342e;\">{WebUtility.HtmlEncode(task.Description)}</p>";

        var subject = $"{T("email.task-assigned.subject")}{task.Title}";
        var html = ShellHtml(
            $@"
              <p style=""margin:0 0 12px;"">{WebUtility.HtmlEncode(T("email.task-assigned.greeting"))} <strong>{WebUtility.HtmlEncode(recipientName)}</strong>,</p>
              <p style=""margin:0 0 12px;""><strong>{WebUtility.HtmlEncode(actorName)}</strong> {WebUtility.HtmlEncode(T("email.task-assigned.body"))}</p>
              <div style=""border-left:3px solid #C96442;padding:8px 12px;background:#FBF2E1;border-radius:0 8px 8px 0;margin:12px 0 18px;"">
                <p style=""margin:0;font-weight:700;font-size:16px;color:#1f1c19;"">{WebUtility.HtmlEncode(task.Title)}</p>
                {description}
                {dueLine}
              </div>
              <p style=""margin:0 0 16px;"">
                <a href=""{WebUtility.HtmlEncode(appBaseUrl + pathPrefix + "/tasks")}"" style=""display:inline-block;padding:10px 18px;background:#C96442;color:#ffffff;border-radius:24px;text-decoration:none;font-weight:600;"">
                  {WebUtility.HtmlEncode(T("email.task-assigned.cta"))}
                </a>
              </p>",
            lang);

        return (subject, html);
    }

    /// <summary>Subject + HTML for the "you were mentioned on the wall" email.</summary>
    /// <param name="actorName">Author of the message/comment.</param>
    /// <param name="recipientName">Person being notified.</param>
    /// <param name="contextLabel">Already-localised label of the context (e.g. "a wall message"). See <see cref="WallMentionContextLabel"/>.</param>
    /// <param name="snippet">Plain-text excerpt of the markdown source, already trimmed.</param>
    /// <param name="appBaseUrl">Public origin used to build the in-app link.</param>
    /// <param name="lang">BCP-47 language tag of the recipient.</param>
    public static (string Subject, string HtmlBody) WallMention(
        string actorName,
        string recipientName,
        string contextLabel,
        string snippet,
        string appBaseUrl,
        string lang)
    {
        var T = (string key) => BackendLocalization.T(key, lang);
        var pathPrefix = BackendLocalization.PathPrefix(lang);

        var subject = $"{actorName}{T("email.wall-mention.subject-suffix")}";
        var html = ShellHtml(
            $@"
              <p style=""margin:0 0 12px;"">{WebUtility.HtmlEncode(T("email.wall-mention.greeting"))} <strong>{WebUtility.HtmlEncode(recipientName)}</strong>,</p>
              <p style=""margin:0 0 12px;""><strong>{WebUtility.HtmlEncode(actorName)}</strong> {WebUtility.HtmlEncode(T("email.wall-mention.body-prefix"))} {WebUtility.HtmlEncode(contextLabel)}:</p>
              <blockquote style=""margin:12px 0 18px;padding:8px 12px;border-left:3px solid #C96442;background:#FBF2E1;border-radius:0 8px 8px 0;color:#3a342e;font-style:italic;"">
                {WebUtility.HtmlEncode(snippet)}
              </blockquote>
              <p style=""margin:0 0 16px;"">
                <a href=""{WebUtility.HtmlEncode(appBaseUrl + pathPrefix + "/wall")}"" style=""display:inline-block;padding:10px 18px;background:#C96442;color:#ffffff;border-radius:24px;text-decoration:none;font-weight:600;"">
                  {WebUtility.HtmlEncode(T("email.wall-mention.cta"))}
                </a>
              </p>",
            lang);

        return (subject, html);
    }

    /// <summary>Resolve the human label for a wall-mention context kind.</summary>
    /// <param name="kind">Either <c>"message"</c> or <c>"comment"</c>.</param>
    /// <param name="lang">Recipient locale.</param>
    public static string WallMentionContextLabel(string kind, string lang) => kind switch
    {
        "comment" => BackendLocalization.T("email.wall-mention.context.comment", lang),
        _ => BackendLocalization.T("email.wall-mention.context.message", lang),
    };

    /// <summary>Item shown in the "your day" digest section of the morning email.</summary>
    /// <param name="Title">Title of the task / event.</param>
    /// <param name="Detail">Optional context line (time, role, location…).</param>
    public sealed record DigestItem(string Title, string? Detail);

    /// <summary>Aggregate content displayed in a daily digest email.</summary>
    /// <param name="Tasks">Chores scheduled for the day in the family.</param>
    /// <param name="Events">Calendar events of the day where the recipient appears.</param>
    /// <param name="Agenda">Members away from home today (work, travel, regular activities).</param>
    /// <param name="Meals">Today's planned lunch and dinner from the meal planner.</param>
    /// <param name="School">Today's school logistics (holiday badge, bus pickup, extracurriculars).</param>
    /// <param name="Birthdays">Members whose birthday falls today or tomorrow.</param>
    /// <param name="WallMessages">Wall messages published since the last digest.</param>
    public sealed record DigestContent(
        IReadOnlyList<DigestItem> Tasks,
        IReadOnlyList<DigestItem> Events,
        IReadOnlyList<DigestItem> Agenda,
        IReadOnlyList<DigestItem> Meals,
        IReadOnlyList<DigestItem> School,
        IReadOnlyList<DigestItem> Birthdays,
        IReadOnlyList<DigestItem> WallMessages)
    {
        /// <summary>True when no section carries any item.</summary>
        public bool IsEmpty => Tasks.Count == 0
            && Events.Count == 0
            && Agenda.Count == 0
            && Meals.Count == 0
            && School.Count == 0
            && Birthdays.Count == 0
            && WallMessages.Count == 0;
    }

    /// <summary>Render the morning digest email.</summary>
    /// <param name="recipientName">Display name of the recipient.</param>
    /// <param name="content">Pre-built sections.</param>
    /// <param name="appBaseUrl">Public origin used to build the in-app link.</param>
    /// <param name="lang">BCP-47 language tag of the recipient.</param>
    public static (string Subject, string HtmlBody) Digest(
        string recipientName,
        DigestContent content,
        string appBaseUrl,
        string lang)
    {
        var T = (string key) => BackendLocalization.T(key, lang);

        var subject = $"{T("email.digest.subject")} — {BackendLocalization.FormatLongDate(DateTime.Now, lang)}";
        var sb = new StringBuilder();
        sb.Append($@"<p style=""margin:0 0 12px;"">{WebUtility.HtmlEncode(T("email.digest.greeting"))} <strong>{WebUtility.HtmlEncode(recipientName)}</strong>, {WebUtility.HtmlEncode(T("email.digest.intro"))}</p>");

        AppendSection(sb, T("email.digest.section.tasks"), content.Tasks);
        AppendSection(sb, T("email.digest.section.events"), content.Events);
        AppendSection(sb, T("email.digest.section.agenda"), content.Agenda);
        AppendSection(sb, T("email.digest.section.school"), content.School);
        AppendSection(sb, T("email.digest.section.meals"), content.Meals);
        AppendSection(sb, T("email.digest.section.birthdays"), content.Birthdays);
        AppendSection(sb, T("email.digest.section.wall"), content.WallMessages);

        sb.Append($@"
              <p style=""margin:24px 0 0;"">
                <a href=""{WebUtility.HtmlEncode(appBaseUrl + BackendLocalization.PathPrefix(lang))}"" style=""display:inline-block;padding:10px 18px;background:#C96442;color:#ffffff;border-radius:24px;text-decoration:none;font-weight:600;"">
                  {WebUtility.HtmlEncode(T("email.digest.cta"))}
                </a>
              </p>");

        return (subject, ShellHtml(sb.ToString(), lang));
    }

    private static void AppendSection(StringBuilder sb, string title, IReadOnlyList<DigestItem> items)
    {
        if (items.Count == 0) return;

        sb.Append($@"
              <h3 style=""font-family:Georgia,serif;margin:18px 0 8px;color:#1f1c19;"">{WebUtility.HtmlEncode(title)}</h3>
              <ul style=""list-style:none;padding:0;margin:0;"">");
        foreach (var item in items)
        {
            var detail = string.IsNullOrEmpty(item.Detail)
                ? string.Empty
                : $@"<span style=""color:#6b6258;font-size:13px;""> · {WebUtility.HtmlEncode(item.Detail)}</span>";
            sb.Append($@"<li style=""padding:6px 10px;margin:0 0 4px;background:#FBF2E1;border-radius:8px;""><strong>{WebUtility.HtmlEncode(item.Title)}</strong>{detail}</li>");
        }
        sb.Append("</ul>");
    }

    /// <summary>Trim a markdown source down to a one-line preview suitable for an email snippet.</summary>
    /// <param name="markdown">Raw user-typed markdown.</param>
    /// <param name="maxLength">Hard cap (default 240 characters).</param>
    public static string MakeSnippet(string markdown, int maxLength = 240)
    {
        // Collapse whitespace then strip the obvious markdown noise so the
        // snippet reads like a sentence in the email rather than literal source.
        var sb = new StringBuilder(markdown.Length);
        var lastWasSpace = false;
        foreach (var ch in markdown)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace) sb.Append(' ');
                lastWasSpace = true;
                continue;
            }
            // Drop the most common decoration characters; leave words and punctuation.
            if (ch is '*' or '_' or '`' or '#' or '>') continue;
            sb.Append(ch);
            lastWasSpace = false;
        }

        var text = sb.ToString().Trim();
        if (text.Length <= maxLength) return text;
        return text[..(maxLength - 1)] + "…";
    }

    private static string ShellHtml(string innerBody, string lang)
    {
        var htmlLang = lang.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "es";
        var footer = BackendLocalization.T("email.footer", lang);
        return $@"<!doctype html>
<html lang=""{htmlLang}"">
<body style=""margin:0;padding:0;background:#F5EDE2;font-family:system-ui,-apple-system,Segoe UI,Helvetica,Arial,sans-serif;color:#1f1c19;"">
  <div style=""max-width:520px;margin:0 auto;padding:24px;"">
    <p style=""font-size:18px;margin:0 0 20px;"">
      <span style=""display:inline-block;width:32px;height:32px;border-radius:10px;background:#C96442;color:#ffffff;text-align:center;line-height:32px;margin-right:8px;"">🪺</span>
      <strong style=""font-family:Georgia,serif;letter-spacing:-0.01em;"">FamilyNido</strong>
    </p>
    {innerBody}
    <p style=""margin:24px 0 0;color:#9b9388;font-size:12px;border-top:1px solid #e6dccb;padding-top:12px;"">
      {WebUtility.HtmlEncode(footer)}
    </p>
  </div>
</body>
</html>";
    }
}
