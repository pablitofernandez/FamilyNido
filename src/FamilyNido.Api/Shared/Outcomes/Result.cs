using FamilyNido.Api.Shared.Errors;

namespace FamilyNido.Api.Shared.Outcomes;

/// <summary>
/// Discriminated result used by application handlers: either a value or an
/// <see cref="ApplicationError"/>. Keeps error flow explicit without exceptions.
/// </summary>
/// <typeparam name="T">Success value type.</typeparam>
public readonly record struct Result<T>
{
    private readonly T? _value;
    private readonly ApplicationError? _error;

    private Result(T value)
    {
        _value = value;
        _error = null;
        IsSuccess = true;
    }

    private Result(ApplicationError error)
    {
        _value = default;
        _error = error;
        IsSuccess = false;
    }

    /// <summary>True when the operation produced a value.</summary>
    public bool IsSuccess { get; }

    /// <summary>Success value. Throws if accessed on a failure result.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value on a failed Result.");

    /// <summary>Error detail. Throws if accessed on a success result.</summary>
    public ApplicationError Error => !IsSuccess
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on a successful Result.");

    /// <summary>Create a success result.</summary>
    public static Result<T> Success(T value) => new(value);

    /// <summary>Create a failure result.</summary>
    public static Result<T> Failure(ApplicationError error) => new(error);

    /// <summary>Implicit conversion from value — terse success creation.</summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>Implicit conversion from error — terse failure creation.</summary>
    public static implicit operator Result<T>(ApplicationError error) => Failure(error);
}
