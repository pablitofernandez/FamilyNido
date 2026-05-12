using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Invitations;

/// <summary>
/// Slice: anonymous read of an invitation by its raw token. Used by the
/// "/invite/:token" preview screen so the recipient sees who is inviting
/// them and to which family before deciding whether to log in or set a
/// password. Never reveals the recipient email or any other sensitive bit.
/// </summary>
public static class GetInvitationByToken
{
    /// <summary>Query carrying the raw token string.</summary>
    public sealed record Query(string Token) : IRequest<Result<InvitationPreviewDto>>;

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<InvitationPreviewDto>>
    {
        private readonly ApplicationDbContext _db;
        private readonly TimeProvider _timeProvider;

        /// <summary>Primary constructor.</summary>
        public Handler(ApplicationDbContext db, TimeProvider timeProvider)
        {
            _db = db;
            _timeProvider = timeProvider;
        }

        /// <inheritdoc />
        public async Task<Result<InvitationPreviewDto>> HandleAsync(Query request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return ApplicationError.NotFound("invitation.not_found", "Invitation not found.");
            }

            var hash = InvitationToken.Hash(request.Token);

            // Pull the invitation along with the bits the preview screen
            // needs. Single SQL round-trip; we don't filter by lifecycle here
            // because the front wants to show "already used" / "expired"
            // instead of a generic 404 to signal what happened.
            var preview = await _db.Invitations
                .AsNoTracking()
                .Where(i => i.TokenHash == hash)
                .Select(i => new
                {
                    i.Id,
                    i.CreatedBy,
                    i.ConsumedAt,
                    i.RevokedAt,
                    i.ExpiresAt,
                    FamilyName = i.FamilyMember!.Family!.Name,
                    MemberDisplayName = i.FamilyMember!.DisplayName,
                })
                .FirstOrDefaultAsync(ct);

            if (preview is null)
            {
                return ApplicationError.NotFound("invitation.not_found", "Invitation not found.");
            }

            // CreatedBy stores the inviter's user-id (or "system" for
            // bootstrapped rows). Resolve to a display name when possible —
            // null is fine; the front falls back to "alguien te ha invitado".
            string? inviterName = null;
            if (Guid.TryParse(preview.CreatedBy, out var inviterId))
            {
                inviterName = await _db.Users
                    .AsNoTracking()
                    .Where(u => u.Id == inviterId)
                    .Select(u => u.DisplayName)
                    .FirstOrDefaultAsync(ct);
            }

            var now = _timeProvider.GetUtcNow();
            var status =
                preview.RevokedAt is not null ? InvitationStatus.Revoked
                : preview.ConsumedAt is not null ? InvitationStatus.Consumed
                : preview.ExpiresAt <= now ? InvitationStatus.Expired
                : InvitationStatus.Pending;

            return new InvitationPreviewDto(
                FamilyName: preview.FamilyName,
                MemberDisplayName: preview.MemberDisplayName,
                InviterDisplayName: inviterName,
                ExpiresAt: preview.ExpiresAt,
                Status: status);
        }
    }
}

/// <summary>Compact preview returned to anonymous callers on the /invite/:token screen.</summary>
/// <param name="FamilyName">Display name of the inviting family.</param>
/// <param name="MemberDisplayName">Member that will be linked.</param>
/// <param name="InviterDisplayName">Best-effort display name of the admin who created the invitation. Null when CreatedBy is not parseable as a user id.</param>
/// <param name="ExpiresAt">UTC expiration instant.</param>
/// <param name="Status">Lifecycle bucket — front decides which message to show.</param>
public sealed record InvitationPreviewDto(
    string FamilyName,
    string MemberDisplayName,
    string? InviterDisplayName,
    DateTimeOffset ExpiresAt,
    InvitationStatus Status);

/// <summary>Lifecycle bucket of an invitation as seen from the preview endpoint.</summary>
public enum InvitationStatus
{
    /// <summary>Live and redeemable.</summary>
    Pending = 0,
    /// <summary>Already redeemed by some user.</summary>
    Consumed = 1,
    /// <summary>Manually revoked by an admin.</summary>
    Revoked = 2,
    /// <summary>Expired by reaching its TTL without being redeemed.</summary>
    Expired = 3,
}
