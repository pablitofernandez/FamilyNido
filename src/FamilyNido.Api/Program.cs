using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Features.Calendar;
using FamilyNido.Api.Features.Dashboard;
using FamilyNido.Api.Features.Families;
using FamilyNido.Api.Features.FamilyMembers;
using FamilyNido.Api.Features.Files;
using FamilyNido.Api.Features.Health;
using FamilyNido.Api.Features.HouseholdTasks;
using FamilyNido.Api.Features.Integrations;
using FamilyNido.Api.Features.Invitations;
using FamilyNido.Api.Features.Meals;
using FamilyNido.Api.Features.MemberAgenda;
using FamilyNido.Api.Features.Notifications;
using FamilyNido.Api.Features.PublicApi;
using FamilyNido.Api.Features.School;
using FamilyNido.Api.Features.Scores;
using FamilyNido.Api.Features.Wall;
using FamilyNido.Api.Features.Weather;
using FamilyNido.Api.Bootstrap;
using FamilyNido.Api.Options;
using FamilyNido.Api.Shared.Markdown;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Domain.Identity;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Structured logging ─────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, sp, logger) => logger
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(sp));

// ── Options ────────────────────────────────────────────────────────────────
builder.Services.AddOptions<OidcOptions>()
    .Bind(builder.Configuration.GetSection(OidcOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddOptions<FamilyOptions>()
    .Bind(builder.Configuration.GetSection(FamilyOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddOptions<CorsOptions>()
    .Bind(builder.Configuration.GetSection(CorsOptions.SectionName));

builder.Services.AddOptions<FilesOptions>()
    .Bind(builder.Configuration.GetSection(FilesOptions.SectionName));

builder.Services.AddOptions<CalendarOptions>()
    .Bind(builder.Configuration.GetSection(CalendarOptions.SectionName));

builder.Services.AddOptions<EmailOptions>()
    .Bind(builder.Configuration.GetSection(EmailOptions.SectionName));

// ── Persistence (DbContext + TimeProvider) ────────────────────────────────
builder.Services.AddFamilyNidoPersistence(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActorProvider, HttpContextActorProvider>();

// ── Mediator + validators (scan the current assembly) ─────────────────────
builder.Services.AddMediator(typeof(Program).Assembly);
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Markdown renderer is pure + stateless — singleton keeps the Markdig pipeline cached.
builder.Services.AddSingleton<MarkdownRenderer>();

// ── Calendar (Google sync) ────────────────────────────────────────────────
// HttpClientFactory powers the OAuth token endpoint calls; it is also a hard
// dependency of GoogleOAuthService.
builder.Services.AddHttpClient();
builder.Services.AddSingleton<GoogleOAuthService>();
builder.Services.AddSingleton<GoogleCalendarClient>();
builder.Services.AddScoped<CalendarSynchronizer>();
builder.Services.AddHostedService<CalendarSyncBackgroundService>();

// ── Local-credential password hashing ─────────────────────────────────────
// PasswordHasher<User> is stateless and thread-safe — singleton lifetime is
// the right call. Default IdentityV3 (PBKDF2 with SHA-512, 100k iterations).
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

// ── Rate limiting ────────────────────────────────────────────────────────
// Local login is the only password-touching endpoint and the obvious target
// for credential stuffing. 5 attempts per 5-minute window per remote IP is a
// stingy-but-fair limit for a family-sized app.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(AuthEndpoints.LocalLoginRateLimitPolicy, http =>
    {
        var key = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
        });
    });

    // Public API is partitioned by remote IP — the API-key claim isn't
    // available yet because rate limiting runs ahead of authentication, and
    // partitioning by IP also doubles as a defence against enumerating
    // tokens (an attacker firing random keys at the endpoint is still bound
    // by the same bucket). 60 req/min/IP fits the worst legitimate burst —
    // an automation firing a couple of times an hour — with
    // plenty of headroom.
    options.AddPolicy(PublicApiEndpoints.RateLimitPolicy, http =>
    {
        var key = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    });
});

// ── Email ─────────────────────────────────────────────────────────────────
// The sender is selected at startup based on Email:Enabled so handlers don't
// need to branch: when email is off, NullEmailSender silently logs and
// reports Delivered=false, and "copy invitation link" UX is the safety net.
var emailEnabled = builder.Configuration
    .GetSection(EmailOptions.SectionName)
    .GetValue<bool>(nameof(EmailOptions.Enabled));
if (emailEnabled)
{
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, NullEmailSender>();
}

// ── Weather (Open-Meteo) ──────────────────────────────────────────────────
// IMemoryCache is used to keep a 30-min cache of the upstream forecast so the
// dashboard reload-storm of a busy household doesn't hammer api.open-meteo.com.
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<WeatherClient>();

// In-memory queue + drainer for fire-and-forget notification emails.
builder.Services.AddSingleton<EmailDispatchService>();
builder.Services.AddHostedService<EmailDispatchBackgroundService>();
// Scoped because it uses the request-scoped DbContext to resolve recipients.
builder.Services.AddScoped<NotificationService>();
// Daily morning digest scheduler. Wakes every few minutes and respects each family's TZ.
builder.Services.AddHostedService<EmailDigestBackgroundService>();

// ── HTTP pipeline services ─────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// Serialize enums (MemberType, FamilyRole, RecurrenceMode, DayOfWeekMask flags)
// as strings so the Angular side can rely on stable string literals instead of
// ordinals that shift whenever we reorder cases.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// ── Authentication + authorization ────────────────────────────────────────
builder.Services.AddFamilyNidoAuthentication(builder.Configuration, builder.Environment);

// ── E2E test data seeder (Testing env + opt-in flag only) ─────────────────
// Plays no role in dev or prod: only registered when ASPNETCORE_ENVIRONMENT
// is "Testing". Even then it no-ops unless Seed:E2E:Enabled is true. Used
// by the Playwright suite to bootstrap a deterministic family + two adult
// users with local credentials.
if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddOptions<E2ESeedOptions>()
        .Bind(builder.Configuration.GetSection(E2ESeedOptions.SectionName));
    builder.Services.AddHostedService<E2ETestDataSeeder>();
}

// ── Demo data seeder (Development env + opt-in flag only) ────────────────
// Drops a curated, screenshot-friendly scenario (one family, four members,
// tasks, wall posts, meals, school) into an empty database. Off by default
// even in Development. Only registered when ASPNETCORE_ENVIRONMENT is
// "Development" and no-ops unless Seed:Demo:Enabled is true. Never runs in
// production. Used to capture the README assets without touching real data.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOptions<DemoSeedOptions>()
        .Bind(builder.Configuration.GetSection(DemoSeedOptions.SectionName));
    builder.Services.AddHostedService<DemoDataSeeder>();
}

// ── CORS for the Angular SPA during development ───────────────────────────
const string SpaCorsPolicy = "spa";
builder.Services.AddCors(options =>
{
    options.AddPolicy(SpaCorsPolicy, policy =>
    {
        var allowed = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];
        policy.WithOrigins(allowed)
              .WithHeaders("Content-Type", "Authorization", "X-Api-Key", "Accept")
              .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
              .AllowCredentials();
    });
});

// ── Health checks ──────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionStringFactory: sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("Postgres")!,
        name: "postgres",
        tags: ["ready"]);

// ── Forward headers (when running behind Traefik) ─────────────────────────
// Restrict trust to RFC1918 private networks (the Docker bridge + LAN
// segments where Traefik realistically sits) plus loopback. Without this
// any client could spoof X-Forwarded-For and bypass IP-bound rate limits
// or impersonate HTTPS to the framework.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(
        System.Net.IPAddress.Parse("10.0.0.0"), 8));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(
        System.Net.IPAddress.Parse("172.16.0.0"), 12));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(
        System.Net.IPAddress.Parse("192.168.0.0"), 16));
    options.KnownProxies.Add(System.Net.IPAddress.Loopback);
    options.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);
});

var app = builder.Build();

// ── Apply migrations at startup (opt-in) ──────────────────────────────────
var familyOptions = app.Services.GetRequiredService<IOptions<FamilyOptions>>().Value;
if (familyOptions.AutoMigrate)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

// ── Pipeline ───────────────────────────────────────────────────────────────
app.UseForwardedHeaders();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(SpaCorsPolicy);
// Rate limiting is in real units (5 local-login attempts / 5 min per IP).
// The integration test suite shares one loopback IP across hundreds of
// requests, so we skip the middleware in the Testing environment to keep
// tests deterministic. Production and dev are unaffected.
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseRateLimiter();
}
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ──────────────────────────────────────────────────────────────
app.MapAuthEndpoints();
app.MapFamilyMemberEndpoints();
app.MapHouseholdTaskEndpoints();
app.MapWallEndpoints();
app.MapFileEndpoints();
app.MapCalendarEndpoints();
app.MapMealEndpoints();
app.MapInvitationEndpoints();
app.MapNotificationEndpoints();
app.MapFamilyEndpoints();
app.MapWeatherEndpoints();
app.MapHealthEndpoints();
app.MapSchoolEndpoints();
app.MapDashboardEndpoints();
app.MapMemberAgendaEndpoints();
app.MapScoreEndpoints();
app.MapIntegrationEndpoints();
app.MapPublicApiEndpoints();

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

app.Run();

/// <summary>Program entry point (partial enables test access via WebApplicationFactory&lt;Program&gt;).</summary>
public partial class Program;
