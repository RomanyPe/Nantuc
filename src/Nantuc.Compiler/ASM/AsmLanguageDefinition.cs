using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace Nantuc.Compiler.ASM;

/// <summary>
/// Централизованное описание ассемблерного языка (ISA Nantuc).
/// Паттерны регексов строятся из соответствующих FrozenSet,
/// поэтому рассинхронизация между списком слов и паттерном невозможна.
///
/// ВНИМАНИЕ: не заменять на [GeneratedRegex] без source generator —
/// придётся хардкодить паттерн, что создаёт риск drift
/// (см. MiniCLanguageDefinition: Keywords не содержит "new",
/// но KeywordPattern — содержит).
/// </summary>
/// <summary>
/// Централизованное описание ассемблерного языка (ISA Nantuc).
/// Паттерны строятся из <see cref="Mnemonics"/> / <see cref="Registers"/>
/// через общий <see cref="WordRegexBuilder"/>.
/// </summary>
public static class AsmLanguageDefinition
{
    /// <summary>Мнемоники инструкций. Регистронезависимо.</summary>
    public static FrozenSet<string> Mnemonics { get; } =
    [
        "NOP", "END", "RET", "MOV", "ADD", "SUB", "AND", "OR", "XOR",
        "INC", "DEC", "NOT", "PUSH", "POP",
        "LDI", "LOAD", "STORE",
        "LOAD_UNSAFE", "STORE_UNSAFE",
        "JMP", "JZ", "JNZ", "JG", "JL", "CALL",
        "LOAD_IND", "STORE_IND", "LOAD_IND_UNSAFE", "STORE_IND_UNSAFE",
        "IN", "OUT", "SHR", "MULT_INT", "DIV",
        "HALT", "WAKE", "WAKE_INT", "CMP", "TEST",
        "JMP_IND", "CALL_IND", "SHL", "MOD",
        "CASTZ", "CASTS",
    ];

    /// <summary>Имена регистров. Регистронезависимо.</summary>
    public static FrozenSet<string> Registers { get; } =
    [
        "r0", "r1",  "r2",  "r3",  "r4",  "r5",  "r6",  "r7",  "r8",  "r9",
        "r10", "r11", "r12", "r13", "r14", "r15", "r16", "r17", "r18", "r19",
        "r20", "r21", "r22", "r23", "r24", "r25", "r26", "r27",
        "rFL", "rSP", "rHP", "rIP",
    ];

    /// <summary>Регекс для распознавания мнемоник (с word-boundary).</summary>
    public static Regex MnemonicRegex { get; } =
        WordRegexBuilder.Build(Mnemonics, RegexOptions.IgnoreCase);

    /// <summary>Регекс для распознавания регистров (с word-boundary).</summary>
    public static Regex RegisterRegex { get; } =
        WordRegexBuilder.Build(Registers, RegexOptions.IgnoreCase);
}