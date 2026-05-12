using System.Security.Claims;
using FamilyNido.Persistence;

namespace FamilyNido.Api.Features.Auth;

/// <summary>
/// <see cref="ICurrentActorProvider"/> implementation that reads the
/// authenticated subject from the active <see cref="HttpContext"/>. Falls back
/// to <c>"system"</c> when no request is in flight (background work, startup,
/// design-time). Registered by the API composition root.
/// </summary>
public sealed class HttpContextActorProvider : ICurrentActorProvider
{
    private readonly IHttpContextAccessor _accessor;

    /// <summary>Primary constructor.</summary>
    public HttpContextActorProvider(IHttpContextAccessor accessor) => _accessor = accessor;

    /// <inheritdoc />
    public string GetActor()
    {
        var principal = _accessor.HttpContext?.User;
        // Prefer the internal userId claim (Guid) so audit trails are stable
        // across credential rotations (a user could swap OIDC for local at any
        // point, but their User.Id never changes). Fall back to the OIDC sub
        // for sessions issued before the userId claim was introduced, and to
        // "system" outside HTTP requests.
        return principal?.FindFirstValue(CurrentUserContext.UserIdClaimType)
               ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? "system";
    }
}
