using System.Globalization;

namespace FamilyNido.Api.Features.Notifications;

/// <summary>
/// Tiny lookup-based localizer for the handful of server-side strings that
/// reach end users (email subjects/body chunks, integration-generated task
/// titles). The frontend has its own $localize pipeline; this is the
/// deliberately simpler equivalent for the backend.
/// </summary>
/// <remarks>
/// <para>
/// Not a full <c>IStringLocalizer&lt;T&gt;</c>: there's no .resx loader, no
/// fallback chain, no missing-key warnings. Each entry is a switch arm — if
/// you add a new locale you add a new <c>case</c>. Keys are namespaced by
/// origin (<c>email.digest.greeting</c>, <c>weather.code.rain</c>) so the
/// file reads top-down by feature.
/// </para>
/// <para>
/// Locale tags follow BCP-47 with a coarse fallback: any tag whose primary
/// subtag is <c>en</c> resolves to the English entries; everything else
/// (including the default <c>es-ES</c>) resolves to Spanish.
/// </para>
/// </remarks>
public static class BackendLocalization
{
    /// <summary>Lookup the localized string for a key. Returns the Spanish source if the key is unknown.</summary>
    /// <param name="key">Stable key (e.g. <c>email.digest.section.tasks</c>).</param>
    /// <param name="lang">BCP-47 tag from <see cref="Domain.Identity.User.PreferredLanguage"/>.</param>
    public static string T(string key, string lang)
    {
        if (IsEnglish(lang))
        {
            return EnglishOrSpanish(key, english: true);
        }
        return EnglishOrSpanish(key, english: false);
    }

    /// <summary>Format a date in the locale-appropriate long form ("d 'de' MMMM" in Spanish, "MMMM d" in English).</summary>
    public static string FormatLongDate(DateTime date, string lang) => IsEnglish(lang)
        ? date.ToString("MMMM d", CultureInfo.GetCultureInfo("en-US"))
        : date.ToString("d 'de' MMMM", CultureInfo.GetCultureInfo("es-ES"));

    /// <summary>
    /// Build the locale-aware path prefix used in email links: <c>/es</c> or <c>/en</c>.
    /// Mirrors the Angular <c>i18n.locales</c> subPath config in <c>angular.json</c>.
    /// </summary>
    public static string PathPrefix(string lang) => IsEnglish(lang) ? "/en" : "/es";

    /// <summary>Coarse "is the user asking for English?" check based on the BCP-47 primary subtag.</summary>
    private static bool IsEnglish(string lang)
    {
        if (string.IsNullOrEmpty(lang)) return false;
        var dash = lang.IndexOf('-');
        var primary = dash < 0 ? lang : lang[..dash];
        return string.Equals(primary, "en", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Centralised dictionary. Kept as one switch so a translator can scan
    /// the file linearly. Add new keys here and they become available
    /// immediately through <see cref="T(string, string)"/>.
    /// </summary>
    private static string EnglishOrSpanish(string key, bool english) => key switch
    {
        // ── Digest email ───────────────────────────────────────────────────
        "email.digest.subject" =>
            english ? "Your day in FamilyNido" : "Tu día en FamilyNido",
        "email.digest.greeting" =>
            english ? "Good morning" : "Buenos días",
        "email.digest.intro" =>
            english ? "this is your day in a glance." : "este es tu resumen del día.",
        "email.digest.section.tasks" =>
            english ? "🪺 Today's tasks" : "🪺 Tareas para hoy",
        "email.digest.section.events" =>
            english ? "📅 Your agenda" : "📅 Tu agenda",
        "email.digest.section.agenda" =>
            english ? "🚗 Out of the house today" : "🚗 Hoy fuera de casa",
        "email.digest.section.school" =>
            english ? "🎒 School today" : "🎒 Hoy en el cole",
        "email.digest.section.meals" =>
            english ? "🍽️ At the table" : "🍽️ Hoy se come",
        "email.digest.section.birthdays" =>
            english ? "🎂 Birthdays" : "🎂 Cumpleaños",
        "email.digest.section.wall" =>
            english ? "💬 New on the wall" : "💬 Nuevo en el muro",
        "email.digest.cta" =>
            english ? "Open FamilyNido" : "Abrir FamilyNido",

        // ── Task assigned email ────────────────────────────────────────────
        "email.task-assigned.subject" =>
            english ? "New task for you: " : "Nueva tarea para ti: ",
        "email.task-assigned.greeting" =>
            english ? "Hi" : "Hola",
        "email.task-assigned.body" =>
            english ? "has assigned you a task as the responsible:" : "te ha asignado una tarea como responsable:",
        "email.task-assigned.due-prefix" =>
            english ? "Due " : "Para el ",
        "email.task-assigned.cta" =>
            english ? "View in FamilyNido" : "Ver en FamilyNido",

        // ── Wall mention email ─────────────────────────────────────────────
        "email.wall-mention.subject-suffix" =>
            english ? " mentioned you on the wall" : " te ha mencionado en el muro",
        "email.wall-mention.greeting" =>
            english ? "Hi" : "Hola",
        "email.wall-mention.body-prefix" =>
            english ? "mentioned you in" : "te ha mencionado en",
        "email.wall-mention.context.message" =>
            english ? "a wall message" : "un mensaje del muro",
        "email.wall-mention.context.comment" =>
            english ? "a wall comment" : "un comentario del muro",
        "email.wall-mention.cta" =>
            english ? "Go to the wall" : "Ir al muro",

        // ── Shared shell ───────────────────────────────────────────────────
        "email.footer" =>
            english
                ? "You're receiving this email because you have this notification turned on. You can change that in My account."
                : "Recibes este email porque tienes activadas las notificaciones de este tipo. Puedes ajustarlas en Mi cuenta.",

        // ── Weather labels (WMO codes → short label) ──────────────────────
        "weather.code.0" => english ? "Clear" : "Despejado",
        "weather.code.1" => english ? "Mostly clear" : "Mayormente despejado",
        "weather.code.2" => english ? "Partly cloudy" : "Parcialmente nublado",
        "weather.code.3" => english ? "Cloudy" : "Nublado",
        "weather.code.fog" => english ? "Fog" : "Niebla",
        "weather.code.drizzle" => english ? "Drizzle" : "Llovizna",
        "weather.code.freezing-drizzle" => english ? "Freezing drizzle" : "Llovizna helada",
        "weather.code.rain" => english ? "Rain" : "Lluvia",
        "weather.code.freezing-rain" => english ? "Freezing rain" : "Lluvia helada",
        "weather.code.snow" => english ? "Snow" : "Nieve",
        "weather.code.sleet" => english ? "Sleet" : "Aguanieve",
        "weather.code.showers" => english ? "Showers" : "Chubascos",
        "weather.code.snow-showers" => english ? "Snow showers" : "Nevadas",
        "weather.code.thunderstorm" => english ? "Thunderstorm" : "Tormenta",
        "weather.code.thunderstorm-hail" => english ? "Thunderstorm with hail" : "Tormenta con granizo",
        "weather.code.unknown" => english ? "No data" : "Sin datos",

        // Unknown key: surface the key itself in dev so the missing string is
        // visible in the email instead of silently empty. Source-locale fallback
        // would be friendlier but also masks bugs.
        _ => $"[{key}]",
    };
}
