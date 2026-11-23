using System.Runtime.CompilerServices;

namespace Nantuc.Common;

public static class RegTypeExtension
{

    public const int LastValueInt = (int)LastValue;
    public const RegType LastValue = RegType.rIP;


    extension(RegType r)
    {
        /// <summary>
        /// Возвращает числовое значение регистра, приведённое к <see cref="int"/>.
        /// </summary>
        /// <returns>Значение <paramref name="reg"/> как <see cref="int"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Int() => (int)r;
        public uint ToUint() => (uint)r;

        public string AsText => r switch
        {
            RegType.r0 => nameof(RegType.r0),
            RegType.r1 => nameof(RegType.r1),
            RegType.r2 => nameof(RegType.r2),
            RegType.r3 => nameof(RegType.r3),
            RegType.r4 => nameof(RegType.r4),
            RegType.r5 => nameof(RegType.r5),
            RegType.r6 => nameof(RegType.r6),
            RegType.r7 => nameof(RegType.r7),
            RegType.r8 => nameof(RegType.r8),
            RegType.r9 => nameof(RegType.r9),
            RegType.r10 => nameof(RegType.r10),
            RegType.r11 => nameof(RegType.r11),
            RegType.r12 => nameof(RegType.r12),
            RegType.r13 => nameof(RegType.r13),
            RegType.r14 => nameof(RegType.r14),
            RegType.r15 => nameof(RegType.r15),
            RegType.r16 => nameof(RegType.r16),
            RegType.r17 => nameof(RegType.r17),
            RegType.r18 => nameof(RegType.r18),
            RegType.r19 => nameof(RegType.r19),
            RegType.r20 => nameof(RegType.r20),
            RegType.r21 => nameof(RegType.r21),
            RegType.r22 => nameof(RegType.r22),
            RegType.r23 => nameof(RegType.r23),
            RegType.r24 => nameof(RegType.r24),
            RegType.r25 => nameof(RegType.r25),
            RegType.r26 => nameof(RegType.r26),
            RegType.r27 => nameof(RegType.r27),
            RegType.rFL => nameof(RegType.rFL),
            RegType.rSP => nameof(RegType.rSP),
            RegType.rHP => nameof(RegType.rHP),
            RegType.rIP => nameof(RegType.rIP),
            _ => "r?"
        };
    }

}
