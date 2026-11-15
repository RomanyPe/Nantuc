using Nantuc.Common;
using System.Runtime.CompilerServices;

namespace Nantuc.Kernel.ProcessorSystem;

// ============================================================
// Чистые функции преобразования операндов.
// Не зависят от состояния процессора — только от OpCodeSize.
// ============================================================
internal static class InstructionHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong MaskOf(OpCodeSize s) => s switch
    {
        OpCodeSize.S8 => 0x00000000_000000FFUL,
        OpCodeSize.S16 => 0x00000000_0000FFFFUL,
        OpCodeSize.S32 => 0x00000000_FFFFFFFFUL,
        OpCodeSize.S64 => ulong.MaxValue,
        _ => 0
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong SignExtend(ulong v, OpCodeSize s) => s switch
    {
        OpCodeSize.S8 => (ulong)(sbyte)(byte)v,
        OpCodeSize.S16 => (ulong)(short)(ushort)v,
        OpCodeSize.S32 => (ulong)(int)(uint)v,
        OpCodeSize.S64 => v,
        _ => 0
    };
}