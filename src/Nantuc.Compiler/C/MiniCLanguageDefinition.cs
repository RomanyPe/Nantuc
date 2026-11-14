using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace Nantuc.Compiler.C;

/// <summary>
/// Централизованное описание языка C, используемое компилятором и инструментами.
/// </summary>
public static partial class MiniCLanguageDefinition
{
    public static FrozenSet<string> Keywords { get; }
    public static FrozenSet<char> Operators { get; }

    static MiniCLanguageDefinition()
    {
        // Ключевые слова (регистрозависимы, как в C)
        Keywords = ["int", "char", "void", "if", "else", "do", "while", "for", "return",
            "break", "struct", "byte", "ushort", "ulong", "new", "asm", "extern", "continue",
            "short", "long", "uint", "sizeof", "const"];
        // Операторы (одиночные символы)
        Operators = ['+', '-', '*', '/', '%', '=', '!', '<', '>', '&', '|', '^', '~', '#'];

    }

    private const string KeywordPattern = @"\b(int|char|void|if|else|do|while|for|return|break|struct|byte|ushort|ulong|new|asm|extern|continue|short|long|uint|sizeof|const)\b";


    [GeneratedRegex(KeywordPattern, RegexOptions.None)]
    public static partial Regex GetKeywordRegex();
}