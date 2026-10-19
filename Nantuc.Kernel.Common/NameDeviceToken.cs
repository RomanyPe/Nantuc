namespace Nantuc.Kernel.Common;

public readonly struct NameDeviceToken : IEquatable<NameDeviceToken>
{
    public const string UnknownName = "Unknown Device";

    private readonly string? _cachedName;

    public NameDeviceToken(ReadOnlySpan<char> name)
    {
        _cachedName = name.IsEmpty ? null : name.ToString();
    }

    public bool IsUnknown => _cachedName is null;

    public string Name => _cachedName ?? UnknownName;


    public override string ToString() => Name;

    public bool Equals(NameDeviceToken other) =>
        string.Equals(Name, other.Name, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is NameDeviceToken other && Equals(other);

    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(Name);

    public static bool operator ==(NameDeviceToken left, NameDeviceToken right) =>
        left.Equals(right);

    public static bool operator !=(NameDeviceToken left, NameDeviceToken right) =>
        !left.Equals(right);
}