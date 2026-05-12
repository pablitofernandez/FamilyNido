using FamilyNido.Domain.Families;
using FamilyNido.Domain.Identity;
using FamilyNido.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FamilyNido.Api.Bootstrap;

/// <summary>
/// Hosted service that, on startup, ensures a deterministic family + two
/// adult users (tester A as admin, tester B as adult) exist with local
/// credentials. Used solely by the Playwright E2E suite both locally and
/// in CI; production registration is gated on
/// <c>Environment == "Testing"</c> in <c>Program.cs</c>, and the seeder
/// itself bails unless <see cref="E2ESeedOptions.Enabled"/> is true.
/// </summary>
/// <remarks>
/// Idempotent. Safe to run on every startup: existing rows are reused, so
/// repeated runs do not duplicate the family/users and do not rotate
/// already-set passwords.
/// </remarks>
public sealed class E2ETestDataSeeder : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly E2ESeedOptions _options;
    private readonly ILogger<E2ETestDataSeeder> _logger;

    /// <summary>Primary constructor.</summary>
    public E2ETestDataSeeder(
        IServiceProvider services,
        IOptions<E2ESeedOptions> options,
        ILogger<E2ETestDataSeeder> logger)
    {
        _services = services;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        if (string.IsNullOrEmpty(_options.UserAPassword) || string.IsNullOrEmpty(_options.UserBPassword))
        {
            _logger.LogWarning("E2E seeder enabled but Seed:E2E:UserAPassword or UserBPassword is empty — skipping.");
            return;
        }

        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        var family = await db.Families
            .FirstOrDefaultAsync(f => f.Name == _options.FamilyName, cancellationToken);
        if (family is null)
        {
            family = new Family
            {
                Name = _options.FamilyName,
                TimeZone = _options.TimeZone,
            };
            db.Families.Add(family);
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "E2E seeder: created family {FamilyName} ({FamilyId}).",
                family.Name, family.Id);
        }

        await EnsureUserAsync(
            db, hasher, family.Id,
            _options.UserAEmail, _options.UserADisplayName,
            _options.UserAPassword, _options.UserAColorHex,
            FamilyRole.Admin, cancellationToken);

        await EnsureUserAsync(
            db, hasher, family.Id,
            _options.UserBEmail, _options.UserBDisplayName,
            _options.UserBPassword, _options.UserBColorHex,
            FamilyRole.Adult, cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task EnsureUserAsync(
        ApplicationDbContext db,
        IPasswordHasher<User> hasher,
        Guid familyId,
        string email,
        string displayName,
        string password,
        string colorHex,
        FamilyRole role,
        CancellationToken cancellationToken)
    {
        var emailLower = email.ToLowerInvariant();
        var user = await db.Users
            .Include(u => u.Credentials)
            .Include(u => u.FamilyMember)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == emailLower, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Email = email,
                DisplayName = displayName,
                Role = role,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
        }

        var localCred = user.Credentials.FirstOrDefault(c => c.Provider == IdentityProvider.Local);
        if (localCred is null)
        {
            db.UserCredentials.Add(new UserCredential
            {
                UserId = user.Id,
                Provider = IdentityProvider.Local,
                PasswordHash = hasher.HashPassword(user, password),
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        if (user.FamilyMember is null)
        {
            db.FamilyMembers.Add(new FamilyMember
            {
                FamilyId = familyId,
                DisplayName = displayName,
                MemberType = MemberType.Adult,
                ColorHex = colorHex,
                UserId = user.Id,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("E2E seeder: ensured user {Email} ({Role}).", email, role);
    }
}
