using FamilyNido.Api.Shared.Errors;
using Microsoft.AspNetCore.Mvc;

namespace FamilyNido.Api.Shared.Http;

/// <summary>Maps <see cref="ApplicationError"/> values to RFC 7807 ProblemDetails.</summary>
public static class ProblemDetailsExtensions
{
    /// <summary>Convert an application error to a <see cref="ProblemDetails"/> payload.</summary>
    public static ProblemDetails ToProblemDetails(this ApplicationError error) => new()
    {
        Title = error.Code,
        Detail = error.Message,
        Status = error.Kind switch
        {
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        },
        Type = $"https://FamilyNido/errors/{error.Code}",
    };

    /// <summary>Return a typed <see cref="IResult"/> for an application error.</summary>
    public static IResult ToHttpResult(this ApplicationError error)
    {
        var pd = error.ToProblemDetails();
        return Results.Problem(
            detail: pd.Detail,
            title: pd.Title,
            statusCode: pd.Status,
            type: pd.Type);
    }
}
