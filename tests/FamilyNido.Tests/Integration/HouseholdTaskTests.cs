using System.Net;
using System.Net.Http.Json;
using FamilyNido.Domain.HouseholdTasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Tests.Integration;

/// <summary>
/// CRUD + completion-state coverage for /api/household-tasks. Hits create,
/// list, today/week views, complete/undo, archive/restore, deadline ranges
/// and floating tasks.
/// </summary>
public sealed class HouseholdTaskTests : IntegrationTestBase
{
    public HouseholdTaskTests(IntegrationFixture fixture) : base(fixture) { }

    private async Task<TestSeed.FamilyHandle> SeedAdminAsync()
    {
        var handle = await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));
        await TestSeed.LoginAsync(Client, "dan@example.com");
        return handle;
    }

    private static object NewTaskBody(
        string title,
        string recurrence = "None",
        DateOnly? startDate = null,
        DateOnly? dueDate = null,
        bool isFloating = false,
        int points = 5,
        string? description = null,
        string category = "General")
    {
        return new
        {
            title,
            description,
            category,
            recurrence,
            weeklyDays = (string?)null,
            monthlyDay = (int?)null,
            timeOfDay = (string?)null,
            startDate = (startDate ?? DateOnly.FromDateTime(DateTime.UtcNow)).ToString("yyyy-MM-dd"),
            dueDate = dueDate?.ToString("yyyy-MM-dd"),
            responsibleMemberId = (Guid?)null,
            relatedMemberIds = Array.Empty<Guid>(),
            isFloating,
            points,
        };
    }

    [Fact]
    public async Task Create_persists_a_one_off_task()
    {
        await SeedAdminAsync();

        var response = await Client.PostAsJsonAsync("/api/household-tasks/", NewTaskBody("Comprar pan"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var saved = await WithDbAsync(db =>
            db.HouseholdTasks.Where(t => t.Title == "Comprar pan").FirstAsync());
        saved.Recurrence.Should().Be(RecurrenceMode.None);
        saved.Points.Should().Be(5);
        saved.IsArchived.Should().BeFalse();
    }

    [Fact]
    public async Task Create_rejects_points_out_of_range()
    {
        await SeedAdminAsync();

        var response = await Client.PostAsJsonAsync(
            "/api/household-tasks/",
            NewTaskBody("Pintar", points: 99));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_returns_only_non_archived_by_default_but_includes_with_flag()
    {
        await SeedAdminAsync();

        // Create one normal + one to-be-archived.
        await Client.PostAsJsonAsync("/api/household-tasks/", NewTaskBody("Tarea viva"));
        var archivedResp = await Client.PostAsJsonAsync("/api/household-tasks/", NewTaskBody("Tarea archivada"));
        var archivedId = (await ReadAsync<TaskRow>(archivedResp))!.Id;
        await Client.PostAsync($"/api/household-tasks/{archivedId}/archive", content: null);

        var defaultResp = await Client.GetAsync("/api/household-tasks/");
        var defaultPage = await ReadAsync<ListPage>(defaultResp);
        defaultPage!.Items.Select(t => t.Title).Should().BeEquivalentTo(["Tarea viva"]);

        var allResp = await Client.GetAsync("/api/household-tasks/?includeArchived=true");
        var allPage = await ReadAsync<ListPage>(allResp);
        allPage!.Items.Select(t => t.Title).Should().BeEquivalentTo(["Tarea viva", "Tarea archivada"]);
    }

    [Fact]
    public async Task ListTasks_returns_first_page_with_default_size()
    {
        await SeedAdminAsync();
        await SeedManyTasksAsync(30);

        var resp = await Client.GetAsync("/api/household-tasks/");
        var page = await ReadAsync<ListPage>(resp);

        page!.Items.Should().HaveCount(25);
        page.Total.Should().Be(30);
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(25);
    }

    [Fact]
    public async Task ListTasks_returns_second_page_with_remaining_items()
    {
        await SeedAdminAsync();
        await SeedManyTasksAsync(30);

        var resp = await Client.GetAsync("/api/household-tasks/?page=2");
        var page = await ReadAsync<ListPage>(resp);

        page!.Items.Should().HaveCount(5);
        page.Total.Should().Be(30);
        page.Page.Should().Be(2);
    }

    [Fact]
    public async Task ListTasks_search_matches_title()
    {
        await SeedAdminAsync();
        await Client.PostAsJsonAsync("/api/household-tasks/", NewTaskBody("Lavavajillas"));
        await Client.PostAsJsonAsync("/api/household-tasks/", NewTaskBody("Aspirar"));

        var resp = await Client.GetAsync("/api/household-tasks/?search=lava");
        var page = await ReadAsync<ListPage>(resp);

        page!.Items.Select(t => t.Title).Should().BeEquivalentTo(["Lavavajillas"]);
        page.Total.Should().Be(1);
    }

    [Fact]
    public async Task ListTasks_search_matches_description()
    {
        await SeedAdminAsync();
        await Client.PostAsJsonAsync("/api/household-tasks/", NewTaskBody(
            "Limpiar campana", description: "Acordarse de cambiar el filtro de carbón."));
        await Client.PostAsJsonAsync("/api/household-tasks/", NewTaskBody("Otra cosa"));

        var resp = await Client.GetAsync("/api/household-tasks/?search=filtro");
        var page = await ReadAsync<ListPage>(resp);

        page!.Items.Select(t => t.Title).Should().BeEquivalentTo(["Limpiar campana"]);
    }

    [Fact]
    public async Task ListTasks_search_matches_category()
    {
        await SeedAdminAsync();
        await Client.PostAsJsonAsync(
            "/api/household-tasks/", NewTaskBody("Sacar la basura", category: "Hogar"));
        await Client.PostAsJsonAsync(
            "/api/household-tasks/", NewTaskBody("Estudiar", category: "Cole"));

        var resp = await Client.GetAsync("/api/household-tasks/?search=hogar");
        var page = await ReadAsync<ListPage>(resp);

        page!.Items.Select(t => t.Title).Should().BeEquivalentTo(["Sacar la basura"]);
    }

    [Fact]
    public async Task ListTasks_search_is_case_insensitive()
    {
        await SeedAdminAsync();
        await Client.PostAsJsonAsync("/api/household-tasks/", NewTaskBody("Lavavajillas"));

        var lower = await ReadAsync<ListPage>(await Client.GetAsync("/api/household-tasks/?search=lava"));
        var upper = await ReadAsync<ListPage>(await Client.GetAsync("/api/household-tasks/?search=LAVA"));

        upper!.Items.Should().HaveCount(lower!.Items.Count);
        upper.Items.Select(t => t.Title).Should().BeEquivalentTo(lower.Items.Select(t => t.Title));
    }

    [Fact]
    public async Task ListTasks_search_combines_with_pagination()
    {
        await SeedAdminAsync();
        // 10 tasks that match "limpia*", 5 that do not.
        await SeedManyTasksAsync(10, titlePrefix: "Limpiar");
        await SeedManyTasksAsync(5, titlePrefix: "Otra");

        var resp = await Client.GetAsync("/api/household-tasks/?search=limpia&page=1&pageSize=5");
        var page = await ReadAsync<ListPage>(resp);

        page!.Items.Should().HaveCount(5);
        page.Total.Should().Be(10);
        page.Items.Should().OnlyContain(t => t.Title.StartsWith("Limpiar"));
    }

    [Fact]
    public async Task ListTasks_pagesize_is_clamped_to_max()
    {
        await SeedAdminAsync();
        await Client.PostAsJsonAsync("/api/household-tasks/", NewTaskBody("Una"));

        var resp = await Client.GetAsync("/api/household-tasks/?pageSize=500");
        var page = await ReadAsync<ListPage>(resp);

        page!.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task ListTasks_invalid_page_falls_back_to_first()
    {
        await SeedAdminAsync();
        await Client.PostAsJsonAsync("/api/household-tasks/", NewTaskBody("Una"));

        var resp = await Client.GetAsync("/api/household-tasks/?page=-3");
        var page = await ReadAsync<ListPage>(resp);

        page!.Page.Should().Be(1);
        page.Items.Should().HaveCount(1);
    }

    /// <summary>
    /// Seed <paramref name="count"/> tasks via the public API. Sequential POSTs
    /// give a stable monotonic <c>CreatedAt</c> ordering so paginated assertions
    /// don't flicker on tied timestamps.
    /// </summary>
    private async Task SeedManyTasksAsync(int count, string titlePrefix = "Tarea")
    {
        for (var i = 1; i <= count; i++)
        {
            var resp = await Client.PostAsJsonAsync(
                "/api/household-tasks/", NewTaskBody($"{titlePrefix} {i:D3}"));
            resp.StatusCode.Should().Be(HttpStatusCode.Created);
        }
    }

    [Fact]
    public async Task Complete_and_undo_round_trips_persistently()
    {
        await SeedAdminAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        var createdResp = await Client.PostAsJsonAsync("/api/household-tasks/", NewTaskBody("Sacar la basura"));
        var task = (await ReadAsync<TaskRow>(createdResp))!;

        var done = await Client.PostAsync($"/api/household-tasks/{task.Id}/occurrences/{today}/complete", content: null);
        done.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterDone = await WithDbAsync(db =>
            db.TaskCompletions.AnyAsync(c => c.TaskId == task.Id));
        afterDone.Should().BeTrue();

        var undone = await Client.PostAsync($"/api/household-tasks/{task.Id}/occurrences/{today}/undo", content: null);
        undone.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterUndone = await WithDbAsync(db =>
            db.TaskCompletions.AnyAsync(c => c.TaskId == task.Id));
        afterUndone.Should().BeFalse();
    }

    [Fact]
    public async Task Today_view_surfaces_tasks_due_today_and_floating()
    {
        await SeedAdminAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // One due today, one floating, one strictly in the future. The future
        // task uses startDate == dueDate so it doesn't trigger deadline-range
        // semantics (which would surface it every day in the window).
        await Client.PostAsJsonAsync("/api/household-tasks/", NewTaskBody("Hoy", dueDate: today));
        await Client.PostAsJsonAsync("/api/household-tasks/", NewTaskBody("Flotante", isFloating: true));
        var futureDate = today.AddDays(1);
        await Client.PostAsJsonAsync("/api/household-tasks/", NewTaskBody(
            "Mañana", startDate: futureDate, dueDate: futureDate));

        var resp = await Client.GetAsync("/api/household-tasks/today");
        var day = await ReadAsync<DayTasks>(resp);
        var titles = day!.Tasks.Select(t => t.Task.Title).ToList();
        titles.Should().Contain(["Hoy", "Flotante"]);
        titles.Should().NotContain("Mañana");
    }

    [Fact]
    public async Task Deadline_range_appears_every_day_until_completed()
    {
        await SeedAdminAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // StartDate < DueDate triggers the deadline-range semantics.
        await Client.PostAsJsonAsync("/api/household-tasks/", NewTaskBody(
            "Lavar el coche",
            startDate: today,
            dueDate: today.AddDays(3)));

        // Today: shows up.
        var dayResp = await Client.GetAsync("/api/household-tasks/today");
        var day = await ReadAsync<DayTasks>(dayResp);
        day!.Tasks.Should().Contain(t => t.Task.Title == "Lavar el coche");

        // Complete the occurrence: from then on it must not appear in the future.
        var task = day.Tasks.First(t => t.Task.Title == "Lavar el coche").Task;
        await Client.PostAsync(
            $"/api/household-tasks/{task.Id}/occurrences/{today:yyyy-MM-dd}/complete", content: null);

        // Same query again — gone everywhere because deadline-range tasks are
        // "done forever" once any completion exists, exactly like floating.
        var afterResp = await Client.GetAsync("/api/household-tasks/today");
        var after = await ReadAsync<DayTasks>(afterResp);
        after!.Tasks.Should().NotContain(t => t.Task.Title == "Lavar el coche");
    }

    [Fact]
    public async Task Archive_then_restore_round_trips()
    {
        await SeedAdminAsync();
        var createdResp = await Client.PostAsJsonAsync("/api/household-tasks/", NewTaskBody("Cositas"));
        var task = (await ReadAsync<TaskRow>(createdResp))!;

        var archive = await Client.PostAsync($"/api/household-tasks/{task.Id}/archive", content: null);
        archive.StatusCode.Should().Be(HttpStatusCode.OK);
        (await WithDbAsync(db => db.HouseholdTasks.Where(t => t.Id == task.Id).Select(t => t.IsArchived).FirstAsync()))
            .Should().BeTrue();

        var restore = await Client.PostAsync($"/api/household-tasks/{task.Id}/restore", content: null);
        restore.StatusCode.Should().Be(HttpStatusCode.OK);
        (await WithDbAsync(db => db.HouseholdTasks.Where(t => t.Id == task.Id).Select(t => t.IsArchived).FirstAsync()))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Admin_can_reattribute_a_completed_occurrence()
    {
        var admin = await SeedAdminAsync();
        var other = await WithDbAsync(db => TestSeed.SeedMemberAsync(
            db, admin.Family.Id, "María", FamilyNido.Domain.Families.MemberType.Adult));
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        var createdResp = await Client.PostAsJsonAsync("/api/household-tasks/", NewTaskBody("Tirar la basura"));
        var task = (await ReadAsync<TaskRow>(createdResp))!;

        // Admin completes the task themselves first.
        var done = await Client.PostAsync($"/api/household-tasks/{task.Id}/occurrences/{today}/complete", content: null);
        done.StatusCode.Should().Be(HttpStatusCode.OK);

        // Then re-attributes the completion to María.
        var put = await Client.PutAsJsonAsync(
            $"/api/household-tasks/{task.Id}/occurrences/{today}/completion",
            new { completedByMemberId = other.Id, note = (string?)null });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var stored = await WithDbAsync(db =>
            db.TaskCompletions.AsNoTracking().FirstAsync(c => c.TaskId == task.Id));
        stored.CompletedByMemberId.Should().Be(other.Id);
    }

    [Fact]
    public async Task Admin_can_create_a_completion_attributed_to_another_member()
    {
        var admin = await SeedAdminAsync();
        var other = await WithDbAsync(db => TestSeed.SeedMemberAsync(
            db, admin.Family.Id, "María", FamilyNido.Domain.Families.MemberType.Adult));
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        var createdResp = await Client.PostAsJsonAsync("/api/household-tasks/", NewTaskBody("Recoger paquete"));
        var task = (await ReadAsync<TaskRow>(createdResp))!;

        // No prior completion — PUT should upsert one attributed to María.
        var put = await Client.PutAsJsonAsync(
            $"/api/household-tasks/{task.Id}/occurrences/{today}/completion",
            new { completedByMemberId = other.Id, note = (string?)null });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var stored = await WithDbAsync(db =>
            db.TaskCompletions.AsNoTracking().FirstAsync(c => c.TaskId == task.Id));
        stored.CompletedByMemberId.Should().Be(other.Id);
    }

    [Fact]
    public async Task Non_admin_cannot_reattribute_a_completion()
    {
        // Seed a family with an adult (non-admin) caller.
        var handle = await WithDbAsync(db => TestSeed.SeedFamilyAsync(
            db, Fixture.Factory.Services,
            email: "adult@example.com",
            displayName: "Adulto",
            role: FamilyNido.Domain.Families.FamilyRole.Adult));
        await TestSeed.LoginAsync(Client, "adult@example.com");
        var other = await WithDbAsync(db => TestSeed.SeedMemberAsync(
            db, handle.Family.Id, "María", FamilyNido.Domain.Families.MemberType.Adult));
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        var createdResp = await Client.PostAsJsonAsync("/api/household-tasks/", NewTaskBody("Limpiar baño"));
        var task = (await ReadAsync<TaskRow>(createdResp))!;

        var put = await Client.PutAsJsonAsync(
            $"/api/household-tasks/{task.Id}/occurrences/{today}/completion",
            new { completedByMemberId = other.Id, note = (string?)null });
        put.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_attribution_to_unknown_member_returns_400()
    {
        await SeedAdminAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        var createdResp = await Client.PostAsJsonAsync("/api/household-tasks/", NewTaskBody("Pasar aspirador"));
        var task = (await ReadAsync<TaskRow>(createdResp))!;

        var put = await Client.PutAsJsonAsync(
            $"/api/household-tasks/{task.Id}/occurrences/{today}/completion",
            new { completedByMemberId = Guid.NewGuid(), note = (string?)null });
        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Completions_history_returns_entries_sorted_descending()
    {
        var admin = await SeedAdminAsync();
        var other = await WithDbAsync(db => TestSeed.SeedMemberAsync(
            db, admin.Family.Id, "María", FamilyNido.Domain.Families.MemberType.Adult));

        // Daily task that started 5 days ago so the historical dates we
        // attribute below are within the schedule (HasOccurrenceOn rejects
        // anything before StartDate).
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5);
        var createdResp = await Client.PostAsJsonAsync(
            "/api/household-tasks/",
            NewTaskBody("Sacar al perro", recurrence: "Daily", startDate: startDate));
        var task = (await ReadAsync<TaskRow>(createdResp))!;

        // Three days, three different completers — checks ordering and
        // attribution round-trip.
        var d0 = DateOnly.FromDateTime(DateTime.UtcNow);
        var d1 = d0.AddDays(-1);
        var d2 = d0.AddDays(-2);

        foreach (var (date, memberId) in new[]
        {
            (d2, admin.Member.Id),
            (d1, other.Id),
            (d0, admin.Member.Id),
        })
        {
            var put = await Client.PutAsJsonAsync(
                $"/api/household-tasks/{task.Id}/occurrences/{date:yyyy-MM-dd}/completion",
                new { completedByMemberId = memberId, note = (string?)null });
            put.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var historyResp = await Client.GetAsync($"/api/household-tasks/{task.Id}/completions");
        historyResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = (await ReadAsync<List<CompletionEntry>>(historyResp))!;

        history.Should().HaveCount(3);
        history[0].OccurrenceDate.Should().Be(d0.ToString("yyyy-MM-dd"));
        history[1].OccurrenceDate.Should().Be(d1.ToString("yyyy-MM-dd"));
        history[2].OccurrenceDate.Should().Be(d2.ToString("yyyy-MM-dd"));
        history[0].CompletedByMemberId.Should().Be(admin.Member.Id);
        history[1].CompletedByMemberId.Should().Be(other.Id);
    }

    private sealed record CompletionEntry(string OccurrenceDate, Guid? CompletedByMemberId, string CompletedAt, string? Note);

    /// <summary>Compact projection for deserialising task DTOs returned by the API.</summary>
    private sealed record TaskRow(Guid Id, string Title, int Points);

    /// <summary>Envelope returned by GET /api/household-tasks since pagination was added.</summary>
    private sealed record ListPage(List<TaskRow> Items, int Total, int Page, int PageSize);

    /// <summary>"Today" view shape returned by /api/household-tasks/today.</summary>
    private sealed record DayTasks(string Date, List<TaskOnDate> Tasks);
    private sealed record TaskOnDate(TaskRow Task, TaskOccurrence Occurrence);
    private sealed record TaskOccurrence(string TaskId, string OccurrenceDate, bool IsCompleted);
}
