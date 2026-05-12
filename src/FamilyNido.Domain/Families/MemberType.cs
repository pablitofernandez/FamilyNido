namespace FamilyNido.Domain.Families;

/// <summary>
/// Classifies a family member by their relationship to the family unit.
/// Only <see cref="Adult"/> members can be linked to a <c>User</c> and authenticate.
/// Children and "other" members exist as references for assignments (events, health, school…).
/// </summary>
public enum MemberType
{
    /// <summary>Adult member of the family (father, mother, partner). Can have a user account.</summary>
    Adult = 0,

    /// <summary>Child member. Never authenticates, always appears as an assignment target.</summary>
    Child = 1,

    /// <summary>Any other reference person (grandparents, caretakers). Does not authenticate.</summary>
    Other = 2,
}
