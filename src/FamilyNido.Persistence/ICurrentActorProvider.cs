namespace FamilyNido.Persistence;

/// <summary>
/// Supplies the identifier used when stamping audit columns (<c>CreatedBy</c>,
/// <c>UpdatedBy</c>). Kept as an abstraction so <see cref="ApplicationDbContext"/>
/// stays independent of ASP.NET Core. The API layer provides the real
/// implementation that reads from the authenticated principal.
/// </summary>
public interface ICurrentActorProvider
{
    /// <summary>
    /// Returns a short, stable identifier for the caller. Defaults such as
    /// <c>"system"</c> are acceptable for background or pre-auth contexts.
    /// </summary>
    string GetActor();
}
