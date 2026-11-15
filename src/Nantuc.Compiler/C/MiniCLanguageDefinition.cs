using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace Nantuc.Compiler.C;

/// <summary>
/// Централизованное описание языка C, используемое компилятором и инструментами.
///
/// Паттерн регекса строится из <see cref="Keywords"/> — единый source of truth.
/// Попытка захардкодить паттерн через [GeneratedRegex] уже приводила к drift'у:
/// "new" присутствовал в паттерне, но отсутствовал в Keywords.
/// </summary>
public static class MiniCLanguageDefinition
{
    /// <summary>Ключевые слова языка. Регистрозависимо (см. RegexOptions.None).</summary>
    public static FrozenSet<string> Keywords { get; } =
    [
        "int", "char", "void", "if", "else", "do", "while", "for",
        "return", "break", "struct", "byte", "ushort", "ulong",
        "asm", "extern", "continue", "short", "long", "uint",
        "sizeof", "const",
    ];

    /// <summary>Символы, которые лексер считает операторами.</summary>
    public static FrozenSet<char> Operators { get; } =
        ['+', '-', '*', '/', '%', '=', '!', '<', '>', '&', '|', '^', '~', '#'];

    /// <summary>Регекс для распознавания ключевых слов (с word-boundary).</summary>
    public static Regex KeywordRegex { get; } = WordRegexBuilder.Build(Keywords);
}