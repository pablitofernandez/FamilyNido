using FamilyNido.Api.Shared.Markdown;
using FluentAssertions;

namespace FamilyNido.Tests.Wall;

/// <summary>
/// Exercises the safety properties of <see cref="MarkdownRenderer"/>. In particular
/// verifies that raw HTML is escaped rather than passed through — that is the
/// contract the wall relies on for RF-WALL-011.
/// </summary>
public sealed class MarkdownRendererTests
{
    private readonly MarkdownRenderer _renderer = new();

    [Fact]
    public void Emphasis_is_rendered()
    {
        var (md, html) = _renderer.Render("Hola **nido**");

        md.Should().Be("Hola **nido**");
        html.Should().Contain("<strong>nido</strong>");
    }

    [Fact]
    public void Soft_line_break_is_treated_as_hard_break()
    {
        var (_, html) = _renderer.Render("linea1\nlinea2");

        html.Should().Contain("<br");
    }

    [Fact]
    public void Autolink_wraps_bare_url()
    {
        var (_, html) = _renderer.Render("mira https://familynido.example");

        html.Should().Contain("<a href=\"https://familynido.example\"");
    }

    [Fact]
    public void Script_tag_is_escaped_not_executed()
    {
        var (_, html) = _renderer.Render("hola <script>alert('x')</script>");

        html.Should().NotContain("<script>");
        html.Should().Contain("&lt;script&gt;");
    }

    [Fact]
    public void Iframe_is_escaped()
    {
        var (_, html) = _renderer.Render("<iframe src='https://evil'></iframe>");

        html.Should().NotContain("<iframe");
        html.Should().Contain("&lt;iframe");
    }

    [Fact]
    public void Inline_html_attributes_are_escaped()
    {
        var (_, html) = _renderer.Render("<a href=\"javascript:alert(1)\">hola</a>");

        html.Should().NotContain("<a href=\"javascript:");
        html.Should().Contain("&lt;a");
    }

    [Fact]
    public void Null_or_whitespace_produces_empty_output()
    {
        var (md1, html1) = _renderer.Render(null);
        var (md2, html2) = _renderer.Render("   ");

        md1.Should().BeEmpty();
        md2.Should().BeEmpty();
        html1.Should().BeEmpty();
        html2.Should().BeEmpty();
    }

    [Fact]
    public void Input_is_trimmed()
    {
        var (md, _) = _renderer.Render("  hola  ");

        md.Should().Be("hola");
    }
}
