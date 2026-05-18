using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Setup;

/// <summary>
/// Slice for the anonymous <c>GET /api/setup/status</c> probe. Returns whether
/// the instance has been initialised — i.e. whether there's at least one
/// <see cref="Domain.Identity.User"/> on file. The SPA hits this from the
/// bootstrap flow to decide between sending an anonymous visitor to
/// <c>/login</c> or the first-run <c>/setup</c> wizard.
/// </summary>
public static class GetSetupStatus
{
    /// <summary>Query — no inputs. The instance state speaks for itself.</summary>
    public sealed record Query : IRequest<Result<SetupStatusDto>>;

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<SetupStatusDto>>
    {
        private readonly ApplicationDbContext _db;

        /// <summary>Primary constructor.</summary>
        public Handler(ApplicationDbContext db) => _db = db;

        /// <inheritdoc />
        public async Task<Result<SetupStatusDto>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var any = await _db.Users.AnyAsync(cancellationToken);
            return new SetupStatusDto(Initialized: any);
        }
    }
}

/// <summary>Wire shape of the setup status response.</summary>
/// <param name="Initialized">True when at least one user exists; the SPA should route to /login. False → /setup.</param>
public sealed record SetupStatusDto(bool Initialized);
