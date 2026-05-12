using System.Net.Http.Json;
using FluentAssertions;

namespace FamilyNido.Tests.Integration;

/// <summary>
/// Coverage for /api/dashboard/preferences and /api/notifications/preferences.
/// Both follow the same shape: GET returns a reconciled view, PUT replaces.
/// </summary>
public sealed class PreferencesTests : IntegrationTestBase
{
    public PreferencesTests(IntegrationFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Dashboard_GET_returns_default_widget_order_when_user_has_no_row()
    {
        await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));
        await TestSeed.LoginAsync(Client, "dan@example.com");

        var resp = await Client.GetAsync("/api/dashboard/preferences");
        resp.IsSuccessStatusCode.Should().BeTrue();

        var prefs = await ReadAsync<DashPrefsDto>(resp);
        // The default order from DashboardWidgets.DefaultOrder.
        prefs!.Widgets.Should().HaveCountGreaterThanOrEqualTo(8);
        prefs.Widgets.Select(w => w.Id).Should().Contain([
            "weather", "school", "agenda", "tasks", "calendar",
            "meals", "wall", "scores", "birthdays",
        ]);
        prefs.Widgets.Should().AllSatisfy(w => w.Visible.Should().BeTrue());
    }

    [Fact]
    public async Task Dashboard_PUT_persists_the_layout_and_GET_reflects_it()
    {
        await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));
        await TestSeed.LoginAsync(Client, "dan@example.com");

        var put = await Client.PutAsJsonAsync("/api/dashboard/preferences", new
        {
            widgets = new[]
            {
                new { id = "tasks", visible = true },
                new { id = "weather", visible = false },
            },
        });
        put.IsSuccessStatusCode.Should().BeTrue();

        var get = await Client.GetAsync("/api/dashboard/preferences");
        var prefs = await ReadAsync<DashPrefsDto>(get);

        // The two ids the user sent are in the order they sent them; everything
        // else is reconciled from the catalogue with Visible=true.
        prefs!.Widgets[0].Id.Should().Be("tasks");
        prefs.Widgets[0].Visible.Should().BeTrue();
        prefs.Widgets[1].Id.Should().Be("weather");
        prefs.Widgets[1].Visible.Should().BeFalse();
    }

    [Fact]
    public async Task Notifications_GET_returns_defaults()
    {
        await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));
        await TestSeed.LoginAsync(Client, "dan@example.com");

        var resp = await Client.GetAsync("/api/notifications/preferences");
        resp.IsSuccessStatusCode.Should().BeTrue();

        var prefs = await ReadAsync<NotifPrefsDto>(resp);
        // Defaults: everything on (matches the persistence default).
        prefs!.EmailEnabled.Should().BeTrue();
        prefs.DigestEnabled.Should().BeTrue();
        prefs.TaskAssignedEnabled.Should().BeTrue();
        prefs.WallMentionEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Notifications_PUT_persists_toggle_changes()
    {
        await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));
        await TestSeed.LoginAsync(Client, "dan@example.com");

        var put = await Client.PutAsJsonAsync("/api/notifications/preferences", new
        {
            emailEnabled = true,
            digestEnabled = false,
            taskAssignedEnabled = true,
            wallMentionEnabled = false,
        });
        put.IsSuccessStatusCode.Should().BeTrue();

        var get = await Client.GetAsync("/api/notifications/preferences");
        var prefs = await ReadAsync<NotifPrefsDto>(get);
        prefs!.DigestEnabled.Should().BeFalse();
        prefs.WallMentionEnabled.Should().BeFalse();
        prefs.TaskAssignedEnabled.Should().BeTrue();
    }

    private sealed record DashPrefsDto(List<WidgetDto> Widgets);
    private sealed record WidgetDto(string Id, bool Visible);

    private sealed record NotifPrefsDto(
        bool EmailEnabled,
        bool DigestEnabled,
        bool TaskAssignedEnabled,
        bool WallMentionEnabled);
}
