namespace FamilyNido.Domain.Identity;

/// <summary>
/// Explicit override for the time format the SPA renders for this user.
/// <c>null</c> on <see cref="User.TimeFormat"/> means "auto" — let the frontend
/// fall back to the active i18n bundle's native hour cycle (en-US → 12H,
/// es-ES → 24H). Setting an explicit value bypasses that inference everywhere
/// the user views FamilyNido.
/// </summary>
public enum TimeFormatPreference
{
    /// <summary>12-hour clock with AM/PM (e.g. <c>9:42 PM</c>).</summary>
    H12,

    /// <summary>24-hour clock (e.g. <c>21:42</c>).</summary>
    H24,
}
