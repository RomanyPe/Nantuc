using System.Diagnostics;

namespace Nantuc.Kernel.Common;

[DebuggerDisplay("{ToString(),nq}")]
public readonly struct RamSize : IEquatable<RamSize>, IComparable<RamSize>
{
    public const byte MinLog2 = 6;   // 64 B
    public const byte MaxLog2 = 31;  // 512 MiB

    /// <summary>log₂ of the size in bytes.</summary>
    public byte Log2 { get; }

    /// <summary>Size in bytes (2^Log2).</summary>
    public ulong Bytes => 1UL << Log2;

    /// <summary>False for <c>default(RamSize)</c>.</summary>
    public bool IsValid => Log2 is >= MinLog2 and <= MaxLog2;

    private RamSize(byte log2) => Log2 = log2;

    public static RamSize B64 => new(6);
    public static RamSize B128 => new(7);
    public static RamSize B256 => new(8);
    public static RamSize B512 => new(9);
    public static RamSize KB1 => new(10);
    public static RamSize KB2 => new(11);
    public static RamSize KB4 => new(12);
    public static RamSize KB8 => new(13);
    public static RamSize KB16 => new(14);
    public static RamSize KB32 => new(15);
    public static RamSize KB64 => new(16);
    public static RamSize KB128 => new(17);
    public static RamSize KB256 => new(18);
    public static RamSize KB512 => new(19);
    public static RamSize MB1 => new(20);
    public static RamSize MB2 => new(21);
    public static RamSize MB4 => new(22);
    public static RamSize MB8 => new(23);
    public static RamSize MB16 => new(24);
    public static RamSize MB32 => new(25);
    public static RamSize MB64 => new(26);
    public static RamSize MB128 => new(27);
    public static RamSize MB256 => new(28);
    public static RamSize MB512 => new(29);
    public static RamSize GB1 => new(30);
    public static RamSize GB2 => new(31);

    public bool Equals(RamSize other) => Log2 == other.Log2;
    public override bool Equals(object? obj) => obj is RamSize r && Equals(r);
    public override int GetHashCode() => Log2;
    public int CompareTo(RamSize other) => Log2.CompareTo(other.Log2);
    public override string ToString() =>
        IsValid ? $"{Bytes} B (2^{Log2})" : "RamSize(invalid)";

    public static bool operator ==(RamSize a, RamSize b) => a.Equals(b);
    public static bool operator !=(RamSize a, RamSize b) => !a.Equals(b);
    public static bool operator <(RamSize a, RamSize b) => a.Log2 < b.Log2;
    public static bool operator >(RamSize a, RamSize b) => a.Log2 > b.Log2;
    public static bool operator <=(RamSize a, RamSize b) => a.Log2 <= b.Log2;
    public static bool operator >=(RamSize a, RamSize b) => a.Log2 >= b.Log2;
}