using System.Net;
using System.Text.RegularExpressions;
using Markdig;

namespace FamilyNido.Api.Shared.Markdown;

/// <summary>One member that may appear after <c>@</c> in markdown source.</summary>
/// <param name="MemberId">Stable identifier — drives notifications and links.</param>
/// <param name="DisplayName">Exact display name to match (case-insensitive at compare time).</param>
public sealed record MentionCandidate(Guid MemberId, string DisplayName);

/// <summary>Rendered markdown plus the set of family members it mentions.</summary>
/// <param name="Markdown">The trimmed markdown source.</param>
/// <param name="Html">Sanitized HTML; mentions wrapped in <c>&lt;span class="bx-mention"&gt;</c>.</param>
/// <param name="MentionedMemberIds">Distinct ids of every recognised <c>@DisplayName</c>.</param>
public sealed record MarkdownRenderResult(
    string Markdown,
    string Html,
    IReadOnlyList<Guid> MentionedMemberIds);

/// <summary>
/// Renders wall markdown into safe HTML. Wraps a single <see cref="MarkdownPipeline"/>
/// with raw-HTML disabled so any embedded <c>&lt;script&gt;</c> or <c>&lt;iframe&gt;</c>
/// is escaped at source — no downstream sanitizer is needed (RF-WALL-011).
/// </summary>
public sealed class MarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseEmphasisExtras()
        .UseAutoLinks()
        .UseSoftlineBreakAsHardlineBreak()
        .DisableHtml()
        .Build();

    /// <summary>
    /// Render <paramref name="input"/> to <c>(markdown, html)</c>. The markdown string
    /// is the trimmed original; the html is the sanitized rendering — persist both so
    /// the UI can show rendered content without re-rendering and the raw source is
    /// kept for edits.
    /// </summary>
    /// <param name="input">Raw markdown typed by the user.</param>
    /// <returns>Tuple with the cleaned markdown source and the rendered HTML.</returns>
    public (string Markdown, string Html) Render(string? input)
    {
        var clean = (input ?? string.Empty).Trim();
        var html = Markdig.Markdown.ToHtml(clean, _pipeline);
        return (clean, html);
    }

    /// <summary>
    /// Render <paramref name="input"/> and detect <c>@DisplayName</c> references
    /// against <paramref name="candidates"/>. Recognised mentions are wrapped in
    /// <c>&lt;span class="bx-mention" data-member-id="…"&gt;</c> in the HTML so the
    /// UI can style them, and their ids are returned for downstream notification
    /// dispatch. Unknown names stay as plain text.
    /// </summary>
    /// <param name="input">Raw markdown typed by the user.</param>
    /// <param name="candidates">Family members eligible to be mentioned (typically active members of the caller's family).</param>
    /// <returns>Cleaned markdown, decorated HTML and the set of mentioned member ids.</returns>
    public MarkdownRenderResult RenderWithMentions(string? input, IReadOnlyList<MentionCandidate> candidates)
    {
        var clean = (input ?? string.Empty).Trim();
        var html = Markdig.Markdown.ToHtml(clean, _pipeline);

        if (candidates.Count == 0 || string.IsNullOrEmpty(clean))
        {
            return new MarkdownRenderResult(clean, html, []);
        }

        // Longest-first so greedy alternation matches "María José" before "María".
        var sorted = candidates
            .GroupBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(c => c.DisplayName.Length)
            .ToList();

        var alternatives = string.Join("|", sorted.Select(c => Regex.Escape(c.DisplayName)));
        // Boundaries are letters/digits — leading boundary keeps email addresses
        // (foo@bar.com) from matching, trailing keeps "@DanX" from matching "@Dan".
        var pattern = $@"(?<![\p{{L}}\p{{N}}])@({alternatives})(?![\p{{L}}\p{{N}}])";
        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var mentioned = new HashSet<Guid>();
        foreach (Match m in regex.Matches(clean))
        {
            var name = m.Groups[1].Value;
            var member = sorted.First(c =>
                string.Equals(c.DisplayName, name, StringComparison.OrdinalIgnoreCase));
            mentioned.Add(member.MemberId);
        }

        // Re-build pattern over HTML-encoded names because Markdig encodes "<&>"
        // before we touch the output. Most names won't change, but this keeps us
        // safe for names with apostrophes or accented characters Markdig touches.
        var htmlSorted = sorted
            .Select(c => new { c.MemberId, c.DisplayName, Encoded = WebUtility.HtmlEncode(c.DisplayName) })
            .ToList();
        var htmlAlternatives = string.Join("|", htmlSorted.Select(c => Regex.Escape(c.Encoded)));
        var htmlPattern = $@"(?<![\p{{L}}\p{{N}}])@({htmlAlternatives})(?![\p{{L}}\p{{N}}])";
        var htmlRegex = new Regex(htmlPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var decoratedHtml = htmlRegex.Replace(html, match =>
        {
            var encodedName = match.Groups[1].Value;
            var member = htmlSorted.First(c =>
                string.Equals(c.Encoded, encodedName, StringComparison.OrdinalIgnoreCase));
            return $"<span class=\"bx-mention\" data-member-id=\"{member.MemberId:D}\">@{member.Encoded}</span>";
        });

        return new MarkdownRenderResult(clean, decoratedHtml, mentioned.ToArray());
    }
}
