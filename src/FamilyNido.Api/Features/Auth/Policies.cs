using FamilyNido.Api.Features.Integrations;
using FamilyNido.Domain.Families;
using Microsoft.AspNetCore.Authorization;

namespace FamilyNido.Api.Features.Auth;

/// <summary>
/// Named authorization policies used across endpoints. Centralizing them keeps
/// policy names discoverable and prevents magic-string drift.
/// </summary>
public static class Policies
{
    /// <summary>Admin of the family — can manage members and configuration.</summary>
    public const string Admin = "family.admin";

    /// <summary>Adult — default access level for authenticated users.</summary>
    public const string Adult = "family.adult";

    /// <summary>Any authenticated and linked user (includes guests).</summary>
    public const string AuthenticatedUser = "family.member";

    /// <summary>External integration callers authenticated by an API key.</summary>
    public const string Integration = "family.integration";

    /// <summary>Registers the policies above against the supplied builder.</summary>
    public static AuthorizationBuilder AddFamilyNidoPolicies(this AuthorizationBuilder builder)
    {
        builder
            .AddPolicy(Admin, p => p.RequireRole(nameof(FamilyRole.Admin)))
            .AddPolicy(Adult, p => p.RequireRole(nameof(FamilyRole.Admin), nameof(FamilyRole.Adult)))
            .AddPolicy(AuthenticatedUser, p => p.RequireAuthenticatedUser())
            .AddPolicy(Integration, p => p
                .AddAuthenticationSchemes(IntegrationApiKeyDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireClaim(IntegrationClaimTypes.FamilyId)
                .RequireClaim(IntegrationClaimTypes.AuthorMemberId));
        return builder;
    }
}
