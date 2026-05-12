using FamilyNido.Domain.Agenda;
using FamilyNido.Domain.Calendar;
using FamilyNido.Domain.Common;
using FamilyNido.Domain.Families;
using FamilyNido.Domain.Files;
using FamilyNido.Domain.Health;
using FamilyNido.Domain.HouseholdTasks;
using FamilyNido.Domain.Identity;
using FamilyNido.Domain.Integrations;
using FamilyNido.Domain.Meals;
using FamilyNido.Domain.Notifications;
using FamilyNido.Domain.School;
using FamilyNido.Domain.Wall;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Persistence;

/// <summary>
/// Primary EF Core context. Owns connection string, entity configurations,
/// and cross-cutting behaviors like audit-column population.
/// </summary>
public sealed class ApplicationDbContext : DbContext
{
    private readonly ICurrentActorProvider? _actorProvider;
    private readonly TimeProvider _timeProvider;

    /// <summary>Primary constructor used by DI.</summary>
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        TimeProvider timeProvider,
        ICurrentActorProvider? actorProvider = null)
        : base(options)
    {
        _timeProvider = timeProvider;
        _actorProvider = actorProvider;
    }

    /// <summary>Design-time / migration-time constructor.</summary>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
        _timeProvider = TimeProvider.System;
    }

    /// <summary>Families persisted in the instance (typically one).</summary>
    public DbSet<Family> Families => Set<Family>();

    /// <summary>Every person belonging to any family, authenticable or not.</summary>
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();

    /// <summary>Authenticable accounts linked to a <see cref="FamilyMember"/>.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Authentication credentials (OIDC subject or local password hash) per user.</summary>
    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();

    /// <summary>Pending or consumed invitations issued by admins to onboard new members.</summary>
    public DbSet<Invitation> Invitations => Set<Invitation>();

    /// <summary>Shared household chores with per-occurrence completion tracking.</summary>
    public DbSet<HouseholdTask> HouseholdTasks => Set<HouseholdTask>();

    /// <summary>Completion markers (one row per task occurrence).</summary>
    public DbSet<TaskCompletion> TaskCompletions => Set<TaskCompletion>();

    /// <summary>Binary assets (wall images today; health/recipe attachments later).</summary>
    public DbSet<FileAsset> FileAssets => Set<FileAsset>();

    /// <summary>Wall posts — the family "pizarra de cocina".</summary>
    public DbSet<WallMessage> WallMessages => Set<WallMessage>();

    /// <summary>1-level replies to wall posts.</summary>
    public DbSet<WallComment> WallComments => Set<WallComment>();

    /// <summary>Emoji reactions to wall posts.</summary>
    public DbSet<WallReaction> WallReactions => Set<WallReaction>();

    /// <summary>Google accounts linked by adult users to mirror their calendar.</summary>
    public DbSet<GoogleAccount> GoogleAccounts => Set<GoogleAccount>();

    /// <summary>Specific Google calendars discovered under a linked account.</summary>
    public DbSet<LinkedCalendar> LinkedCalendars => Set<LinkedCalendar>();

    /// <summary>Mirrored Google Calendar events imported by the periodic sync engine.</summary>
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();

    /// <summary>Weekly meal-plan slots — one row per (family, date, slot).</summary>
    public DbSet<MealPlanSlot> MealPlanSlots => Set<MealPlanSlot>();

    /// <summary>Per-user notification toggles (email digest, task-assigned, etc.).</summary>
    public DbSet<UserNotificationPreferences> UserNotificationPreferences => Set<UserNotificationPreferences>();

    /// <summary>Per-user dashboard widget visibility + order.</summary>
    public DbSet<UserDashboardPreferences> UserDashboardPreferences => Set<UserDashboardPreferences>();

    /// <summary>Audit rows tracking that the daily digest has been processed for a (user, local date).</summary>
    public DbSet<EmailDigestRun> EmailDigestRuns => Set<EmailDigestRun>();

    /// <summary>Lightweight medical card per family member (1:1).</summary>
    public DbSet<HealthProfile> HealthProfiles => Set<HealthProfile>();

    /// <summary>Vaccination history per family member.</summary>
    public DbSet<Vaccination> Vaccinations => Set<Vaccination>();

    /// <summary>Active or past medications taken by family members.</summary>
    public DbSet<Medication> Medications => Set<Medication>();

    /// <summary>Lightweight school card per family member (1:1).</summary>
    public DbSet<SchoolProfile> SchoolProfiles => Set<SchoolProfile>();

    /// <summary>Weekly default of who takes / picks up each kid at school (drop-off + pick-up).</summary>
    public DbSet<SchoolDaySchedule> SchoolDaySchedules => Set<SchoolDaySchedule>();

    /// <summary>Per-date overrides or cancellations of the daily school commute.</summary>
    public DbSet<SchoolDayException> SchoolDayExceptions => Set<SchoolDayException>();

    /// <summary>Family-wide school holiday ranges that cancel bus + extracurriculars.</summary>
    public DbSet<SchoolHoliday> SchoolHolidays => Set<SchoolHoliday>();

    /// <summary>After-school activities per kid with their default caretakers.</summary>
    public DbSet<Extracurricular> Extracurriculars => Set<Extracurricular>();

    /// <summary>Per-date overrides and cancellations of extracurricular sessions.</summary>
    public DbSet<ExtracurricularException> ExtracurricularExceptions => Set<ExtracurricularException>();

    /// <summary>Recurring weekly entries in each member's agenda (work, gym, regular travel).</summary>
    public DbSet<MemberAgendaPattern> MemberAgendaPatterns => Set<MemberAgendaPattern>();

    /// <summary>Per-date overrides and ad-hoc additions to the recurring agenda.</summary>
    public DbSet<MemberAgendaException> MemberAgendaExceptions => Set<MemberAgendaException>();

    /// <summary>Long-lived API tokens used by external integrations (scripts, automatizaciones).</summary>
    public DbSet<IntegrationApiKey> IntegrationApiKeys => Set<IntegrationApiKey>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    /// <inheritdoc />
    public override int SaveChanges()
    {
        StampAuditColumns();
        return base.SaveChanges();
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditColumns();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampAuditColumns()
    {
        var now = _timeProvider.GetUtcNow();
        var actor = _actorProvider?.GetActor() ?? "system";

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = actor;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = actor;
                    break;
            }
        }
    }
}
