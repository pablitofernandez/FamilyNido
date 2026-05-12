using FamilyNido.Domain.Families;

namespace FamilyNido.Api.Features.Invitations;

/// <summary>
/// Read model returned by admin endpoints. Never exposes the raw token —
/// "copy link" UX uses the link returned by <see cref="CreateInvitationResponse"/>
/// at creation time only.
/// </summary>
/// <param name="Id">Stable invitation id.</param>
/// <param name="FamilyMemberId">Member that will be linked.</param>
/// <param name="MemberDisplayName">Display name of the target member.</param>
/// <param name="Email">Recipient email.</param>
/// <param name="RoleOnAccept">Role granted when accepted.</param>
/// <param name="ExpiresAt">UTC instant after which the token is unusable.</param>
/// <param name="CreatedAt">UTC instant of creation.</param>
public sealed record InvitationDto(
    Guid Id,
    Guid FamilyMemberId,
    string MemberDisplayName,
    string Email,
    FamilyRole RoleOnAccept,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt);

/// <summary>
/// Compact invitation summary embedded in <see cref="FamilyMembers.FamilyMemberDto"/>
/// so the roster screen can render "pending: dan@..." without a second
/// roundtrip per member.
/// </summary>
/// <param name="Id">Invitation id.</param>
/// <param name="Email">Recipient email.</param>
/// <param name="ExpiresAt">UTC expiration instant.</param>
public sealed record PendingInvitationDto(Guid Id, string Email, DateTimeOffset ExpiresAt);

/// <summary>
/// Response of <c>POST /api/invitations</c>. Carries the freshly minted link
/// so the admin can copy it manually if email delivery failed.
/// </summary>
/// <param name="Invitation">Persisted invitation read model.</param>
/// <param name="MemberId">Linked family member id (existing or just created).</param>
/// <param name="CopyLink">Absolute URL the recipient must visit to accept.</param>
/// <param name="EmailDelivered">True when the SMTP relay accepted the message.</param>
public sealed record CreateInvitationResponse(
    InvitationDto Invitation,
    Guid MemberId,
    string CopyLink,
    bool EmailDelivered);
