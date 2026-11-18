using Nantuc.Diagnostics;
using System.Collections.Frozen;

namespace Nantuc.Compiler.C.Lexer;

// ============================================================
// Lexer — разбор исходника на токены.
//
// Partial-файлы по зонам ответственности:
//   - Lexer.cs             — поля, Tokenize(), позиционирование (этот файл)
//   - Lexer.Trivia.cs      — пробелы и комментарии
//   - Lexer.Literals.cs    — идентификаторы, числа, строки, символы
//   - Lexer.Operators.cs   — операторы (табличный разбор)
// ============================================================
public partial class Lexer(string source, ErrorReporter errorReporter)
{
    private readonly string _source = source;
    private readonly ErrorReporter _erRep = errorReporter;
    private readonly List<Token> _tokens = [];

    private int _position;
    private int _line = 1;
    private int _column = 1;

    private static readonly FrozenSet<char> Punctuation =
        ['(', ')', '{', '}', '[', ']', ';', ',', '.', ':'];

    public List<Token> Tokenize()
    {
        while (_position < _source.Length)
        {
            char c = _source[_position];

            if (char.IsWhiteSpace(c)) { SkipWhitespace(); continue; }
            if (c == '/' && Peek() == '/') { SkipLineComment(); continue; }
            if (c == '/' && Peek() == '*') { SkipBlockComment(); continue; }

            if (char.IsLetter(c) || c == '_') { ReadIdentifier(); continue; }
            if (char.IsDigit(c)) { ReadNumber(); continue; }
            if (c == '"') { ReadString(); continue; }
            if (c == '\'') { ReadChar(); continue; }
            if (MiniCLanguageDefinition.Operators.Contains(c)) { ReadOperator(); continue; }

            if (Punctuation.Contains(c))
            {
                _tokens.Add(new Token(TokenType.Punctuation, c.ToString(), _line, _column));
                Advance();
                continue;
            }

            _erRep.Throw(ErrorCode.Lexer_UnexpectedCharacter, 
                DiagnosticData.Of(c), 
                DiagnosticData.Of(_line), 
                DiagnosticData.Of(_column));
        }

        _tokens.Add(new Token(TokenType.EOF, "", _line, _column));
        return _tokens;
    }

    // ---------- Позиционирование ----------
    private char Peek(int offset = 1)
        => _position + offset < _source.Length ? _source[_position + offset] : '\0';

    /// <summary>Сдвиг на 1 символ без учёта переноса строки (для токенов без '\n').</summary>
    private void Advance()
    {
        _position++; 
        _column++;
    }

    /// <summary>Сдвиг на N символов без учёта переноса строки (для escape-последовательностей).</summary>
    private void Advance(int count)
    {
        _position += count; 
        _column += count;
    }
}