using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;

namespace FamilyNido.Api.Features.Auth;

/// <summary>Slice for <c>GET /api/auth/me</c>: returns the profile of the caller.</summary>
public static class GetMe
{
    /// <summary>Query carrying no input — the caller is resolved from the ambient context.</summary>
    public sealed record Query : IRequest<Result<MeDto>>;

    /// <summary>Builds the profile DTO from <see cref="ICurrentUserContext"/>.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<MeDto>>
    {
        private readonly ICurrentUserContext _userContext;

        /// <summary>Primary constructor.</summary>
        public Handler(ICurrentUserContext userContext) => _userContext = userContext;

        /// <inheritdoc />
        public async Task<Result<MeDto>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden(
                    "auth.not_linked",
                    "Your account is not linked to a family member. Ask the family admin to link you.");
            }

            return new MeDto(
                UserId: current.User.Id,
                Email: current.User.Email,
                DisplayName: current.User.DisplayName,
                Role: current.User.Role,
                FamilyId: current.Family.Id,
                FamilyName: current.Family.Name,
                MemberId: current.Member.Id,
                MemberDisplayName: current.Member.DisplayName,
                ColorHex: current.Member.ColorHex,
                PhotoPath: current.Member.PhotoPath,
                PreferredLanguage: current.User.PreferredLanguage,
                TimeFormat: current.User.TimeFormat,
                TemperatureUnit: current.User.TemperatureUnit);
        }
    }
}
