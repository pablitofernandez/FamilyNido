using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;

namespace FamilyNido.Api.Features.Families;

/// <summary>
/// Slice for <c>GET /api/family</c>. Returns the caller's family profile —
/// any authenticated member of the family can read it.
/// </summary>
public static class GetMyFamily
{
    /// <summary>Query carries no payload; the caller resolves the family.</summary>
    public sealed record Query : IRequest<Result<FamilyDto>>;

    /// <summary>Resolves the family through <see cref="ICurrentUserContext"/>.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<FamilyDto>>
    {
        private readonly ICurrentUserContext _userContext;

        /// <summary>Primary constructor.</summary>
        public Handler(ICurrentUserContext userContext) => _userContext = userContext;

        /// <inheritdoc />
        public async Task<Result<FamilyDto>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family.");
            }

            var f = current.Family;
            return new FamilyDto(f.Id, f.Name, f.TimeZone, f.Locale, f.Latitude, f.Longitude, f.LocationLabel);
        }
    }
}
