using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace Compiller.C;

/// <summary>
/// Централизованное описание языка C, используемое компилятором и инструментами.
/// </summary>
public static partial class MiniCLanguageDefinition
{
    public static readonly FrozenSet<string> Keywords;
    public static readonly FrozenSet<char> Operators;
    public static readonly FrozenSet<char> Punctuation;

    private static readonly Regex _keywordRegex;

    static MiniCLanguageDefinition()
    {
        // Ключевые слова (регистрозависимы, как в C)
        Keywords = ["int", "char", "void", "if", "else", "while", "for", "return",
            "break", "struct", "byte", "ushort", "ulong", "new", "asm", "extern"];

        // Операторы (одиночные символы)
        Operators = ['+', '-', '*', '/', '%', '=', '!', '<', '>', '&', '|', '^', '~', '#'];

        // Знаки пунктуации
        Punctuation = ['(', ')', '{', '}', '[', ']', ';', ',', '.', ':'];

        // Построение регулярных выражений после инициализации коллекций
        _keywordRegex = new(GetKeywordPattern(), RegexOptions.Compiled);
    }

    /// <summary>
    /// Паттерн для поиска ключевых слов.
    /// </summary>
    public static string GetKeywordPattern() =>
        $@"\b({string.Join("|", Keywords)})\b";

    /// <summary>
    /// Паттерн для поиска операторов – символьный класс из экранированных символов.
    /// </summary>
    public static string GetOperatorsPattern()
    {
        string escapedChars = string.Concat(Operators.Select(c => Regex.Escape(c.ToString())));
        return $"[{escapedChars}]";
    }

    /// <summary>
    /// Паттерн для поиска пунктуации – символьный класс.
    /// </summary>
    public static string GetPunctuationPattern()
    {
        string escapedChars = string.Concat(Punctuation.Select(c => Regex.Escape(c.ToString())));
        return $"[{escapedChars}]";
    }

    public static Regex GetKeywordRegex() => _keywordRegex;
}