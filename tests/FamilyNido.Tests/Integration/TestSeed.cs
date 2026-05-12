using System.Net.Http.Json;
using FamilyNido.Domain.Families;
using FamilyNido.Domain.Identity;
using FamilyNido.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyNido.Tests.Integration;

/// <summary>
/// Helpers for assembling the minimum domain graph an integration test needs:
/// a family, a user with a known local password, and one or more linked
/// members. Returned by reference so tests can chain seeding calls and assert
/// the persisted state.
/// </summary>
public static class TestSeed
{
    /// <summary>Default password used by every seeded user. Hashed with PBKDF2 v3 by the password hasher.</summary>
    public const string DefaultPassword = "Test1234!";

    /// <summary>Compact bundle returned by <see cref="SeedFamilyAsync"/>.</summary>
    public sealed record FamilyHandle(Family Family, FamilyMember Member, User User);

    /// <summary>
    /// Persist a family + an authenticable user with a linked member. Defaults
    /// produce an admin adult; pass <paramref name="role"/> to vary.
    /// </summary>
    public static async Task<FamilyHandle> SeedFamilyAsync(
        ApplicationDbContext db,
        IServiceProvider services,
        string email = "dan@example.com",
        string displayName = "Dan",
        FamilyRole role = FamilyRole.Admin,
        string colorHex = "#C96442")
    {
        var hasher = services.GetRequiredService<IPasswordHasher<User>>();

        var family = new Family { Name = "Familia Test", TimeZone = "Europe/Madrid" };
        db.Families.Add(family);

        var user = new User { Email = email, DisplayName = displayName, Role = role };
        db.Users.Add(user);

        var member = new FamilyMember
        {
            FamilyId = family.Id,
            DisplayName = displayName,
            MemberType = MemberType.Adult,
            ColorHex = colorHex,
            UserId = user.Id,
        };
        db.FamilyMembers.Add(member);

        var credential = new UserCredential
        {
            UserId = user.Id,
            Provider = IdentityProvider.Local,
            PasswordHash = hasher.HashPassword(user, DefaultPassword),
        };
        db.UserCredentials.Add(credential);

        await db.SaveChangesAsync();
        return new FamilyHandle(family, member, user);
    }

    /// <summary>Append one extra member (not authenticable) to an existing family.</summary>
    public static async Task<FamilyMember> SeedMemberAsync(
        ApplicationDbContext db,
        Guid familyId,
        string displayName,
        MemberType type = MemberType.Child,
        string colorHex = "#7BA4A8",
        DateOnly? birthDate = null)
    {
        var member = new FamilyMember
        {
            FamilyId = familyId,
            DisplayName = displayName,
            MemberType = type,
            ColorHex = colorHex,
            BirthDate = birthDate,
        };
        db.FamilyMembers.Add(member);
        await db.SaveChangesAsync();
        return member;
    }

    /// <summary>
    /// Sign the supplied client in via <c>POST /api/auth/local/login</c>. The
    /// session cookie is preserved on the client because <c>HandleCookies</c>
    /// is enabled on the test factory.
    /// </summary>
    public static async Task LoginAsync(HttpClient client, string email, string password = DefaultPassword)
    {
        var response = await client.PostAsJsonAsync("/api/auth/local/login", new { email, password });
        response.IsSuccessStatusCode.Should().BeTrue($"login should succeed (got {response.StatusCode})");
    }
}
