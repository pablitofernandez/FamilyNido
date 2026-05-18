namespace FamilyNido.Domain.Identity;

/// <summary>
/// Explicit override for the temperature unit shown to this user in the
/// weather widget. <c>null</c> on <see cref="User.TemperatureUnit"/> means
/// "auto" — the frontend derives the unit from the active i18n bundle
/// (en-US → Fahrenheit, everything else → Celsius). Setting an explicit
/// value bypasses that inference.
/// </summary>
public enum TemperatureUnitPreference
{
    /// <summary>Celsius (°C). Default in metric locales.</summary>
    Celsius,

    /// <summary>Fahrenheit (°F). Default for users in the US.</summary>
    Fahrenheit,
}
