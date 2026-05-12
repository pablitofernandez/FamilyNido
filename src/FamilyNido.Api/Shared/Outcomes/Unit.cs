namespace FamilyNido.Api.Shared.Outcomes;

/// <summary>Void placeholder returned by <see cref="Result{T}"/> when there is no value.</summary>
public readonly record struct Unit
{
    /// <summary>Canonical singleton instance.</summary>
    public static Unit Value { get; }
}
