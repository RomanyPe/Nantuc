using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace Nantuc.Compiler;

// ============================================================
// Построение \b(...)\b-паттерна из FrozenSet<string>.
// Единая точка для AsmLanguageDefinition и MiniCLanguageDefinition,
// чтобы формы не разъезжались.
//
// ВАЖНО: не заменять на [GeneratedRegex] — придётся хардкодить
// паттерн, что уже один раз привело к drift'у (см. историю
// MiniCLanguageDefinition: "new" был в паттерне, но не в Keywords).
// ============================================================
internal static class WordRegexBuilder
{
    /// <summary>
    /// Строит регекс, матчащий любую из строк <paramref name="words"/>
    /// по границам слова (\b).
    ///
    /// Порядок альтернатив неважен: \b с обеих сторон гарантирует,
    /// что \b(r1|r10)\b не матчит "r10" как "r1" (после "1" идёт "0"
    /// — тоже word char, \b не срабатывает).
    /// </summary>
    public static Regex Build(FrozenSet<string> words, RegexOptions options = RegexOptions.None)
        => new($@"\b({string.Join("|", words)})\b", options);
}
