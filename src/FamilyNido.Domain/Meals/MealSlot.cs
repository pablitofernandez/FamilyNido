namespace FamilyNido.Domain.Meals;

/// <summary>
/// Slot of the day a meal entry belongs to. v1 only models the two slots that
/// the family actually plans (comida y cena); breakfast/snack would land here
/// later as additional cases without breaking persistence (the column is a
/// readable string).
/// </summary>
public enum MealSlot
{
    /// <summary>"Comida" — midday main meal in the Spanish family schedule.</summary>
    Lunch = 0,

    /// <summary>"Cena" — evening main meal.</summary>
    Dinner = 1,
}
