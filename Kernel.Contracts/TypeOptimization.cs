using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Kernel.Contracts;

/// <summary>
/// Stable identifier for an optimization pass or event.
/// Well-known identifiers are exposed as static fields; user-defined
/// identifiers can be created via the constructor or <see cref="TryParse"/>.
/// Every constructed instance is valid; the only way to obtain an invalid
/// instance is <c>default(OptimizationId)</c>.
/// </summary>
/// <remarks>
/// <para>
/// The value is a lowercase, ASCII string of <c>[a-z0-9.-]</c>, at most
/// <see cref="MaxLength"/> characters long, e.g. <c>"peephole"</c>,
/// <c>"inline.func"</c>, <c>"node.removed.before-inline"</c>.
/// </para>
/// <para>
/// Well-known identifiers are guaranteed stable across library versions
/// and are safe to persist (logs, caches, config files). Never rename a
/// well-known id after release — introduce a new one instead.
/// </para>
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
public readonly struct OptimizationId : IEquatable<OptimizationId>, IComparable<OptimizationId>
{
    /// <summary>Maximum allowed length of an identifier.</summary>
    public const int MaxLength = 128;

    /// <summary>
    /// Raw identifier, or <c>null</c> for <c>default(OptimizationId)</c>.
    /// </summary>
    public string? Value { get; }

    /// <summary>False only for <c>default(OptimizationId)</c>.</summary>
    public bool IsValid => Value is not null;

    /// <summary>
    /// Creates an identifier. Throws if <paramref name="value"/> is not a
    /// valid id.
    /// </summary>
    public OptimizationId(string value)
    {
        if (!IsValidId(value))
            throw new ArgumentException(
                $"Invalid optimization id: '{value}'. " +
                $"Expected 1..{MaxLength} chars of [a-z0-9.-].",
                nameof(value));
        Value = value;
    }

    /// <summary>
    /// Non-throwing counterpart of the constructor, for parsing user input.
    /// </summary>
    public static bool TryParse(string? value,
                               [NotNullWhen(true)] out OptimizationId id)
    {
        if (IsValidId(value))
        {
            id = new OptimizationId(value!);
            return true;
        }
        id = default;
        return false;
    }

    private static bool IsValidId([NotNullWhen(true)] string? value)
    {
        if (value is null || value.Length == 0 || value.Length > MaxLength)
            return false;

        foreach (char c in value)
        {
            if (c is >= 'a' and <= 'z') continue;
            if (c is >= '0' and <= '9') continue;
            if (c is '.' or '-') continue;
            return false;
        }
        return true;
    }

    // ---------------------------------------------------------------------
    // Well-known ids (stable; never rename after release)
    // ---------------------------------------------------------------------

    public static readonly OptimizationId Peephole = new("peephole");

    // ---------------------------------------------------------------------
    // Equality / comparison / formatting
    // ---------------------------------------------------------------------

    public bool Equals(OptimizationId other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is OptimizationId o && Equals(o);

    public override int GetHashCode() =>
        Value?.GetHashCode() ?? 0;

    public int CompareTo(OptimizationId other) =>
        string.CompareOrdinal(Value, other.Value);

    public override string ToString() =>
        Value ?? "OptimizationId(invalid)";

    // ---------------------------------------------------------------------
    // Operators
    // ---------------------------------------------------------------------

    public static bool operator ==(OptimizationId a, OptimizationId b) => a.Equals(b);
    public static bool operator !=(OptimizationId a, OptimizationId b) => !a.Equals(b);
    public static bool operator <(OptimizationId a, OptimizationId b) => a.CompareTo(b) < 0;
    public static bool operator >(OptimizationId a, OptimizationId b) => a.CompareTo(b) > 0;
    public static bool operator <=(OptimizationId a, OptimizationId b) => a.CompareTo(b) <= 0;
    public static bool operator >=(OptimizationId a, OptimizationId b) => a.CompareTo(b) >= 0;
}