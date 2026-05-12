using FluentValidation;

namespace FamilyNido.Api.Shared.Http;

/// <summary>Minimal endpoint helpers for FluentValidation.</summary>
public static class ValidationExtensions
{
    /// <summary>
    /// Validates the payload using FluentValidation and returns an
    /// <see cref="IResult"/> with ProblemDetails on failure, or <c>null</c> on success.
    /// </summary>
    public static async Task<IResult?> ValidateOrProblemAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        if (result.IsValid)
        {
            return null;
        }

        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        return Results.ValidationProblem(errors);
    }
}
