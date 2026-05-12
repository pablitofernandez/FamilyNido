using FamilyNido.Api.Features.Invitations;
using FamilyNido.Domain.Families;
using FamilyNido.Domain.Identity;

namespace FamilyNido.Api.Features.FamilyMembers;

/// <summary>
/// Read-model projection of a <see cref="FamilyMember"/> returned by the API.
/// </summary>
/// <param name="Id">Stable member identifier.</param>
/// <param name="DisplayName">Name shown across the UI.</param>
/// <param name="MemberType">Kind of member (adult/child/other).</param>
/// <param name="ColorHex">Consistent color for this member in calendar and avatars.</param>
/// <param name="BirthDate">Date of birth if known.</param>
/// <param name="ContactEmail">Informational contact email, not the OIDC email.</param>
/// <param name="PhotoPath">Relative path to the avatar file, if any.</param>
/// <param name="IsActive">Whether the member is active or archived.</param>
/// <param name="HasAccount">True when a user account is linked.</param>
/// <param name="Role">Authorization role if the member has an account, else null.</param>
/// <param name="PendingInvitation">Pending (non-consumed, non-revoked, non-expired) invitation, if any.</param>
public sealed record FamilyMemberDto(
    Guid Id,
    string DisplayName,
    MemberType MemberType,
    string ColorHex,
    DateOnly? BirthDate,
    string? ContactEmail,
    string? PhotoPath,
    bool IsActive,
    bool HasAccount,
    FamilyRole? Role,
    PendingInvitationDto? PendingInvitation)
{
    /// <summary>Project a domain entity to the DTO shape used by the API.</summary>
    public static FamilyMemberDto From(FamilyMember m, Invitation? pending = null) => new(
        Id: m.Id,
        DisplayName: m.DisplayName,
        MemberType: m.MemberType,
        ColorHex: m.ColorHex,
        BirthDate: m.BirthDate,
        ContactEmail: m.ContactEmail,
        PhotoPath: m.PhotoPath,
        IsActive: m.IsActive,
        HasAccount: m.UserId is not null,
        Role: m.User?.Role,
        PendingInvitation: pending is null ? null : new PendingInvitationDto(pending.Id, pending.Email, pending.ExpiresAt));
}
