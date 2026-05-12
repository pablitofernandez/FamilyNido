using FamilyNido.Domain.Families;

namespace FamilyNido.Api.Features.Auth;

/// <summary>Profile payload returned by <c>GET /api/auth/me</c>.</summary>
/// <param name="UserId">Internal user identifier.</param>
/// <param name="Email">Email associated with the account.</param>
/// <param name="DisplayName">Name shown in the UI.</param>
/// <param name="Role">Authorization role.</param>
/// <param name="FamilyId">Owning family identifier.</param>
/// <param name="FamilyName">Owning family display name.</param>
/// <param name="MemberId">Linked family member id, if any.</param>
/// <param name="MemberDisplayName">Linked family member display name.</param>
/// <param name="ColorHex">Linked family member color code.</param>
/// <param name="PhotoPath">Relative path to the member's avatar image, if any. Used by the shell to load the photo via <c>/api/family-members/{id}/photo</c>.</param>
/// <param name="PreferredLanguage">BCP-47 tag (e.g. <c>es-ES</c>, <c>en-US</c>) the user picked for the UI. Drives email + integration content.</param>
public sealed record MeDto(
    Guid UserId,
    string Email,
    string DisplayName,
    FamilyRole Role,
    Guid FamilyId,
    string FamilyName,
    Guid? MemberId,
    string? MemberDisplayName,
    string? ColorHex,
    string? PhotoPath,
    string PreferredLanguage);
