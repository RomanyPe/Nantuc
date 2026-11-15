using Nantuc.Common;
using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace Nantuc.Compiler.ASM;

/// <summary>
/// Централизованное описание ассемблерного языка (ISA Nantuc).
///
/// Единственный source of truth — <see cref="MnemonicMap"/> и <see cref="RegisterMap"/>.
/// Множества <see cref="Mnemonics"/> / <see cref="Registers"/> и регексы
/// строятся из ключей этих словарей, поэтому drift невозможен.
///
/// Reflection не используется намеренно: карты явные, чтобы работать
/// в NativeAOT/trimming и без аллокаций на горячем пути ассемблера.
/// </summary>
public static class AsmLanguageDefinition
{
    /// <summary>Мнемоника → OpCode. Регистронезависимо.</summary>
    public static FrozenDictionary<string, OpCode> MnemonicMap { get; } =
        new Dictionary<string, OpCode>(StringComparer.OrdinalIgnoreCase)
        {
            ["NOP"] = OpCode.NOP,
            ["END"] = OpCode.END,
            ["RET"] = OpCode.RET,
            ["MOV"] = OpCode.MOV,
            ["ADD"] = OpCode.ADD,
            ["SUB"] = OpCode.SUB,
            ["AND"] = OpCode.AND,
            ["OR"] = OpCode.OR,
            ["XOR"] = OpCode.XOR,
            ["INC"] = OpCode.INC,
            ["DEC"] = OpCode.DEC,
            ["NOT"] = OpCode.NOT,
            ["PUSH"] = OpCode.PUSH,
            ["POP"] = OpCode.POP,
            ["LDI"] = OpCode.LDI,
            ["LOAD"] = OpCode.LOAD,
            ["STORE"] = OpCode.STORE,
            ["JMP"] = OpCode.JMP,
            ["JZ"] = OpCode.JZ,
            ["JNZ"] = OpCode.JNZ,
            ["JG"] = OpCode.JG,
            ["JL"] = OpCode.JL,
            ["CALL"] = OpCode.CALL,
            ["LOAD_IND"] = OpCode.LOAD_IND,
            ["STORE_IND"] = OpCode.STORE_IND,
            ["IN"] = OpCode.IN,
            ["OUT"] = OpCode.OUT,
            ["CALL_IND"] = OpCode.CALL_IND,
            ["JMP_IND"] = OpCode.JMP_IND,
            ["SHR"] = OpCode.SHR,
            ["SHL"] = OpCode.SHL,
            ["MULT_INT"] = OpCode.MULT_INT,
            ["DIV"] = OpCode.DIV,
            ["MOD"] = OpCode.MOD,
            ["HALT"] = OpCode.HALT,
            ["WAKE"] = OpCode.WAKE,
            ["WAKE_INT"] = OpCode.WAKE_INT,
            ["LOAD_IND_UNSAFE"] = OpCode.LOAD_IND_UNSAFE,
            ["LOAD_UNSAFE"] = OpCode.LOAD_UNSAFE,
            ["STORE_IND_UNSAFE"] = OpCode.STORE_IND_UNSAFE,
            ["STORE_UNSAFE"] = OpCode.STORE_UNSAFE,
            ["CMP"] = OpCode.CMP,
            ["TEST"] = OpCode.TEST,
            ["CASTZ"] = OpCode.CASTZ,
            ["CASTS"] = OpCode.CASTS,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>Имя регистра → RegType. Регистронезависимо.</summary>
    public static FrozenDictionary<string, RegType> RegisterMap { get; } =
        new Dictionary<string, RegType>(StringComparer.OrdinalIgnoreCase)
        {
            ["r0"] = RegType.r0,
            ["r1"] = RegType.r1,
            ["r2"] = RegType.r2,
            ["r3"] = RegType.r3,
            ["r4"] = RegType.r4,
            ["r5"] = RegType.r5,
            ["r6"] = RegType.r6,
            ["r7"] = RegType.r7,
            ["r8"] = RegType.r8,
            ["r9"] = RegType.r9,
            ["r10"] = RegType.r10,
            ["r11"] = RegType.r11,
            ["r12"] = RegType.r12,
            ["r13"] = RegType.r13,
            ["r14"] = RegType.r14,
            ["r15"] = RegType.r15,
            ["r16"] = RegType.r16,
            ["r17"] = RegType.r17,
            ["r18"] = RegType.r18,
            ["r19"] = RegType.r19,
            ["r20"] = RegType.r20,
            ["r21"] = RegType.r21,
            ["r22"] = RegType.r22,
            ["r23"] = RegType.r23,
            ["r24"] = RegType.r24,
            ["r25"] = RegType.r25,
            ["r26"] = RegType.r26,
            ["r27"] = RegType.r27,
            ["rFL"] = RegType.rFL,
            ["rSP"] = RegType.rSP,
            ["rHP"] = RegType.rHP,
            ["rIP"] = RegType.rIP,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    // ------------------------------------------------------------------
    // Производные множества и регексы — из ключей карт (никакого drift'а).
    // ------------------------------------------------------------------

    /// <summary>Мнемоники инструкций (ключи <see cref="MnemonicMap"/>).</summary>
    public static FrozenSet<string> Mnemonics { get; } = MnemonicMap.Keys.ToFrozenSet();

    /// <summary>Имена регистров (ключи <see cref="RegisterMap"/>).</summary>
    public static FrozenSet<string> Registers { get; } = RegisterMap.Keys.ToFrozenSet();

    /// <summary>Регекс для распознавания мнемоник (с word-boundary).</summary>
    public static Regex MnemonicRegex { get; } = WordRegexBuilder.Build(Mnemonics);

    /// <summary>Регекс для распознавания регистров (с word-boundary).</summary>
    public static Regex RegisterRegex { get; } = WordRegexBuilder.Build(Registers);
}