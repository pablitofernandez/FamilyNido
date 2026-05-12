using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Integrations;
using FamilyNido.Persistence;
using FluentValidation;

namespace FamilyNido.Api.Features.Integrations;

/// <summary>
/// Slice for <c>POST /api/integrations/api-keys</c> — admin mints a new token.
/// The plaintext is returned exactly once; only the digest stays in the DB.
/// </summary>
public static class CreateIntegrationApiKey
{
    /// <summary>Command carrying the desired display name.</summary>
    /// <param name="Name">Human-readable label, e.g. "my automation". 1..80 chars.</param>
    public sealed record Command(string Name) : IRequest<Result<CreatedIntegrationApiKeyDto>>;

    /// <summary>Validates the input.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator.</summary>
        public Validator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .MaximumLength(80);
        }
    }

    /// <summary>Generates the token, persists the digest, returns the plaintext exactly once.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<CreatedIntegrationApiKeyDto>>
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserContext _userContext;

        /// <summary>Primary constructor.</summary>
        public Handler(ApplicationDbContext db, ICurrentUserContext userContext)
        {
            _db = db;
            _userContext = userContext;
        }

        /// <inheritdoc />
        public async Task<Result<CreatedIntegrationApiKeyDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var plaintext = IntegrationTokens.Generate();
            var key = new IntegrationApiKey
            {
                FamilyId = current.Family.Id,
                AuthorMemberId = current.Member.Id,
                Name = request.Name.Trim(),
                TokenHash = IntegrationTokens.Hash(plaintext),
                Prefix = IntegrationTokens.PrefixOf(plaintext),
            };

            _db.IntegrationApiKeys.Add(key);
            await _db.SaveChangesAsync(cancellationToken);

            var dto = new IntegrationApiKeyDto(
                key.Id, key.Name, key.Prefix, key.CreatedAt, key.LastUsedAt, key.RevokedAt);
            return new CreatedIntegrationApiKeyDto(plaintext, dto);
        }
    }
}
