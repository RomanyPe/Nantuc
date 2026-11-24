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
    private static FrozenDictionary<string, OpCode> BuildOpCodeMap()
    {
        int count = OpCodeExtension.LastValueInt + 1;
        var items = new KeyValuePair<string, OpCode>[count];
        for (int i = 0; i < count; i++)
        {
            var op = (OpCode)i;
            items[i] = new KeyValuePair<string, OpCode>(op.AsText, op);
        }

        return items.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
    private static FrozenDictionary<string, RegType> BuildRegisterMap()
    {
        int count = RegTypeExtension.LastValueInt + 1;
        var items = new KeyValuePair<string, RegType>[count];
        for (int i = 0; i < count; i++)
        {
            var op = (RegType)i;
            items[i] = new KeyValuePair<string, RegType>(op.AsText, op);
        }
        return items.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
    /// <summary>Мнемоника → OpCode. Регистронезависимо.</summary>
    public static FrozenDictionary<string, OpCode> MnemonicMap { get; } = BuildOpCodeMap();

    /// <summary>Имя регистра → RegType. Регистронезависимо.</summary>
    public static FrozenDictionary<string, RegType> RegisterMap { get; } = BuildRegisterMap();

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