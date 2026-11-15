namespace Nantuc.Compiler.C.Lexer;

public partial class Lexer
{
    // ============================================================
    // Пропуск пробелов и комментариев (с корректным учётом '\n').
    // ============================================================
    private void SkipWhitespace()
    {
        while (_position < _source.Length && char.IsWhiteSpace(_source[_position]))
            AdvanceTrackingLine();
    }

    private void SkipLineComment()
    {
        Advance(2);                                     // "//"
        while (_position < _source.Length && _source[_position] != '\n')
            Advance();
    }

    private void SkipBlockComment()
    {
        Advance(2);                                     // "/*"
        while (_position < _source.Length - 1
               && !(_source[_position] == '*' && _source[_position + 1] == '/'))
        {
            AdvanceTrackingLine();
        }
        Advance(2);                                     // "*/"
    }

    /// <summary>Сдвиг на 1 символ с корректным учётом '\n'.</summary>
    private void AdvanceTrackingLine()
    {
        if (_source[_position] == '\n') { _line++; _column = 1; }
        else _column++;
        _position++;
    }
}