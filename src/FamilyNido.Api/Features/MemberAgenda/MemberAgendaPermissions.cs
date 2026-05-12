using FamilyNido.Api.Features.Auth;
using FamilyNido.Domain.Families;

namespace FamilyNido.Api.Features.MemberAgenda;

/// <summary>
/// Centralised admin-or-self check used by every write slice in the agenda
/// module. Admin can edit anyone's agenda; an authenticated user can edit
/// their own (the member they are linked to).
/// </summary>
internal static class MemberAgendaPermissions
{
    /// <summary>True when <paramref name="current"/> can mutate <paramref name="targetMemberId"/>'s agenda.</summary>
    public static bool CanWrite(CurrentUser? current, Guid targetMemberId)
    {
        if (current is null) return false;
        if (current.User.Role == FamilyRole.Admin) return true;
        return current.Member.Id == targetMemberId;
    }
}
