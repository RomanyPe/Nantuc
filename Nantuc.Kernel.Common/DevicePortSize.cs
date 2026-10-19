using System.Diagnostics;

namespace Nantuc.Kernel.Common;

/// <summary>
/// Size of a single device's port block, stored as log₂(bytes).
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
public readonly struct DevicePortSize : IEquatable<DevicePortSize>, IComparable<DevicePortSize>
{
    public const byte MinLog2 = 3;   // 8 B
    public const byte MaxLog2 = 6;   // 64 B

    public byte Log2 { get; }
    public ulong Bytes => 1UL << Log2;
    public bool IsValid => Log2 is >= MinLog2 and <= MaxLog2;

    private DevicePortSize(byte log2) => Log2 = log2;

    public static DevicePortSize B8 => new(3);
    public static DevicePortSize B16 => new(4);
    public static DevicePortSize B32 => new(5);
    public static DevicePortSize B64 => new(6);
    public static DevicePortSize B128 => new(7);

    public bool Equals(DevicePortSize other) => Log2 == other.Log2;
    public override bool Equals(object? obj) => obj is DevicePortSize p && Equals(p);
    public override int GetHashCode() => Log2;
    public int CompareTo(DevicePortSize other) => Log2.CompareTo(other.Log2);
    public override string ToString() => IsValid ? $"{Bytes} B (2^{Log2})" : "DevicePortSize(invalid)";

    public static bool operator ==(DevicePortSize a, DevicePortSize b) => a.Equals(b);
    public static bool operator !=(DevicePortSize a, DevicePortSize b) => !a.Equals(b);
    public static bool operator <(DevicePortSize a, DevicePortSize b) => a.Log2 < b.Log2;
    public static bool operator >(DevicePortSize a, DevicePortSize b) => a.Log2 > b.Log2;
    public static bool operator <=(DevicePortSize a, DevicePortSize b) => a.Log2 <= b.Log2;
    public static bool operator >=(DevicePortSize a, DevicePortSize b) => a.Log2 >= b.Log2;
}
