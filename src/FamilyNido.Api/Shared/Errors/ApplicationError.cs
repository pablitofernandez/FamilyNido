namespace FamilyNido.Api.Shared.Errors;

/// <summary>
/// Structured error returned by application handlers. Maps cleanly to HTTP
/// ProblemDetails without leaking infrastructure details.
/// </summary>
/// <param name="Code">Machine-readable error code (e.g. "family_member.not_found").</param>
/// <param name="Message">Human-readable message, safe to surface to the user.</param>
/// <param name="Kind">Error kind that maps to a default HTTP status.</param>
public sealed record ApplicationError(string Code, string Message, ErrorKind Kind)
{
    /// <summary>Requested resource does not exist.</summary>
    public static ApplicationError NotFound(string code, string message)
        => new(code, message, ErrorKind.NotFound);

    /// <summary>Caller is authenticated but lacks rights to perform the action.</summary>
    public static ApplicationError Forbidden(string code, string message)
        => new(code, message, ErrorKind.Forbidden);

    /// <summary>Input is structurally invalid or violates a domain rule.</summary>
    public static ApplicationError Validation(string code, string message)
        => new(code, message, ErrorKind.Validation);

    /// <summary>Operation conflicts with the current state (e.g. would break an invariant).</summary>
    public static ApplicationError Conflict(string code, string message)
        => new(code, message, ErrorKind.Conflict);
}

/// <summary>Kinds of error that differ in their HTTP mapping.</summary>
public enum ErrorKind
{
    /// <summary>400 Bad Request.</summary>
    Validation,

    /// <summary>403 Forbidden.</summary>
    Forbidden,

    /// <summary>404 Not Found.</summary>
    NotFound,

    /// <summary>409 Conflict.</summary>
    Conflict,
}
