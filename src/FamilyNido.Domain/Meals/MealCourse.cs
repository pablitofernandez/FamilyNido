namespace FamilyNido.Domain.Meals;

/// <summary>
/// Course within a meal slot. Spanish meals typically have a "primer plato"
/// (first course — soup, salad, pasta…) and a "segundo plato" (main — meat,
/// fish, eggs…); either of them may be skipped on a casual day.
/// </summary>
public enum MealCourse
{
    /// <summary>Primer plato — typically a starter, soup or pasta.</summary>
    First = 0,

    /// <summary>Segundo plato — typically the main dish.</summary>
    Second = 1,
}
