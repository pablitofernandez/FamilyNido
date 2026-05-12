namespace FamilyNido.Api.Features.Health;

/// <summary>Aggregate "health card" for a single member returned by GET /api/health/members/{id}.</summary>
/// <param name="MemberId">Family member id.</param>
/// <param name="MemberDisplayName">Convenience copy of the member's name for headers.</param>
/// <param name="Profile">Static profile (blood, allergies, conditions, notes). Null when never saved.</param>
/// <param name="Vaccinations">Vaccinations sorted by date descending.</param>
/// <param name="Medications">Medications sorted by start date descending.</param>
public sealed record MemberHealthDto(
    Guid MemberId,
    string MemberDisplayName,
    HealthProfileDto? Profile,
    IReadOnlyList<VaccinationDto> Vaccinations,
    IReadOnlyList<MedicationDto> Medications);

/// <summary>Body of the static health profile.</summary>
/// <param name="BloodType">"A+", "O-", "AB+"… or null when unknown.</param>
/// <param name="Allergies">Free-text list of allergies (multi-line allowed).</param>
/// <param name="ChronicConditions">Free-text list of chronic conditions.</param>
/// <param name="Notes">Free-text general notes.</param>
public sealed record HealthProfileDto(
    string? BloodType,
    string? Allergies,
    string? ChronicConditions,
    string? Notes);

/// <summary>Single vaccination row.</summary>
/// <param name="Id">Stable id.</param>
/// <param name="Name">Vaccine name.</param>
/// <param name="Date">Date administered.</param>
/// <param name="NextDueDate">Next due date when known.</param>
/// <param name="Notes">Optional notes.</param>
public sealed record VaccinationDto(
    Guid Id,
    string Name,
    DateOnly Date,
    DateOnly? NextDueDate,
    string? Notes);

/// <summary>Single medication row.</summary>
/// <param name="Id">Stable id.</param>
/// <param name="Name">Medication name.</param>
/// <param name="Dose">Dose string (free text).</param>
/// <param name="Frequency">Frequency string (free text).</param>
/// <param name="StartDate">Start date.</param>
/// <param name="EndDate">End date when finite.</param>
/// <param name="Instructions">Optional instructions.</param>
/// <param name="IsActive">Computed: true when EndDate is null or >= today (in UTC).</param>
public sealed record MedicationDto(
    Guid Id,
    string Name,
    string? Dose,
    string? Frequency,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Instructions,
    bool IsActive);
