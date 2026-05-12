namespace FamilyNido.Domain.Families;

/// <summary>
/// Authorization role attached to a <c>User</c>. Only adults with a linked user have a role.
/// </summary>
/// <remarks>
/// Ordered so that higher numeric value implies broader permissions — simplifies policy checks.
/// </remarks>
public enum FamilyRole
{
    /// <summary>Very limited read-only access (e.g. grandparent who only views the calendar).</summary>
    Guest = 0,

    /// <summary>Full access to all modules except critical configuration.</summary>
    Adult = 1,

    /// <summary>Full access plus member management, integrations and destructive operations.</summary>
    Admin = 2,
}
