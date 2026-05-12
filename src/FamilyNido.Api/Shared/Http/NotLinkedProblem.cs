namespace FamilyNido.Api.Shared.Http;

/// <summary>
/// Factory for the "user is authenticated but not linked to a family member"
/// ProblemDetails response (RF-AUTH-003). The front-end uses the <c>title</c>
/// code <c>auth.not_linked</c> to route to the stop page.
/// </summary>
public static class NotLinkedProblem
{
    /// <summary>Return a 403 ProblemDetails with the canonical code.</summary>
    public static IResult Create() => Results.Problem(
        title: "auth.not_linked",
        detail: "Your account is not linked to a family member. Ask the family admin to link you.",
        statusCode: StatusCodes.Status403Forbidden,
        type: "https://FamilyNido/errors/auth.not_linked");
}
