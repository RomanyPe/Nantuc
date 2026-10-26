using Nantuc.Kernel.Diagnostics;
using System.Collections.Frozen;

namespace Nantuc.Compiler.C;

// ============================================================
// 1. ЛЕКСИЧЕСКИЙ АНАЛИЗАТОР (без изменений)
// ============================================================
public enum TokenType
{
    Identifier, Number, String, Char,
    Keyword, Operator, Punctuation,
    Comment, Whitespace, EOF, Arrow   // оператор ->
}

public class Token(TokenType type, string value, int line, int column)
{
    public TokenType Type { get; } = type;
    public string Value { get; } = value;
    public int Line { get; } = line;
    public int Column { get; } = column;

    public override string ToString() => $"{Type}: '{Value}' at {Line}:{Column}";
}

public class Lexer(string source)
{
    private readonly string _source = source;
    private int _position;
    private int _line = 1;
    private int _column = 1;
    private readonly List<Token> _tokens = [];

    private static readonly FrozenSet<char> Punctuation = 
        ['(', ')', '{', '}', '[', ']', ';', ',', '.', ':' ];
    public List<Token> Tokenize()
    {
        while (_position < _source.Length)
        {
            char c = _source[_position];

            if (char.IsWhiteSpace(c))
            {
                SkipWhitespace();
                continue;
            }

            if (c == '/' && Peek() == '/')
            {
                SkipLineComment();
                continue;
            }
            if (c == '/' && Peek() == '*')
            {
                SkipBlockComment();
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                ReadIdentifier();
                continue;
            }
            if (char.IsDigit(c))
            {
                ReadNumber();
                continue;
            }
            if (c == '"')
            {
                ReadString();
                continue;
            }
            if (c == '\'')
            {
                ReadChar();
                continue;
            }
            if (MiniCLanguageDefinition.Operators.Contains(c))
            {
                ReadOperator();
                continue;
            }
            if (Punctuation.Contains(c))
            {
                _tokens.Add(new Token(TokenType.Punctuation, c.ToString(), _line, _column));
                _position++;
                _column++;
                continue;
            }

            ThrowCompiler.Throw(ErrorCode.Lexer_UnexpectedCharacter, c, _line, _column);
        }

        _tokens.Add(new Token(TokenType.EOF, "", _line, _column));
        return _tokens;
    }

    private char Peek(int offset = 1) =>
        _position + offset < _source.Length ? _source[_position + offset] : '\0';

    private void SkipWhitespace()
    {
        while (_position < _source.Length && char.IsWhiteSpace(_source[_position]))
        {
            if (_source[_position] == '\n') { _line++; _column = 1; }
            else _column++;
            _position++;
        }
    }

    private void SkipLineComment()
    {
        _position += 2;
        _column += 2;
        while (_position < _source.Length && _source[_position] != '\n')
        {
            _position++;
            _column++;
        }
    }

    private void SkipBlockComment()
    {
        _position += 2;
        _column += 2;
        while (_position < _source.Length - 1 && !(_source[_position] == '*' && _source[_position + 1] == '/'))
        {
            if (_source[_position] == '\n') { _line++; _column = 1; }
            else _column++;
            _position++;
        }
        _position += 2;
        _column += 2;
    }

    private void ReadIdentifier()
    {
        int start = _position;
        int startCol = _column;
        while (_position < _source.Length && (char.IsLetterOrDigit(_source[_position]) || _source[_position] == '_'))
        {
            _position++;
            _column++;
        }
        string value = _source[start.._position];
        TokenType type = MiniCLanguageDefinition.Keywords.Contains(value) ? TokenType.Keyword : TokenType.Identifier;
        _tokens.Add(new Token(type, value, _line, startCol));
    }

    private void ReadNumber()
    {
        int start = _position;
        int startCol = _column;
        bool isHex = false, isFloat = false;
        if (_source[_position] == '0' && Peek() == 'x')
        {
            isHex = true;
            _position += 2;
            _column += 2;
        }
        while (_position < _source.Length)
        {
            char c = _source[_position];
            if (char.IsDigit(c) || (isHex && ((c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))))
            {
                _position++;
                _column++;
            }
            else if (c == '.' && !isFloat)
            {
                isFloat = true;
                _position++;
                _column++;
            }
            else break;
        }
        string value = _source[start.._position];
        _tokens.Add(new Token(TokenType.Number, value, _line, startCol));
    }

    private void ReadString()
    {
        int start = _position;
        int startCol = _column;
        _position++; _column++;
        while (_position < _source.Length && _source[_position] != '"')
        {
            if (_source[_position] == '\\') { _position += 2; _column += 2; }
            else { _position++; _column++; }
        }
        _position++; _column++;
        _tokens.Add(new Token(TokenType.String, _source[start.._position], _line, startCol));
    }

    private void ReadChar()
    {
        int start = _position;
        int startCol = _column;
        _position++; _column++;
        if (_source[_position] == '\\') { _position += 2; _column += 2; }
        else { _position++; _column++; }
        _position++; _column++;
        _tokens.Add(new Token(TokenType.Char, _source[start.._position], _line, startCol));
    }

    private void ReadOperator()
    {
        int start = _position;
        int startCol = _column;
        char c = _source[_position];
        // Двухсимвольные операторы (одинаковые символы)
        if ((c == '+' || c == '-' || c == '*' || c == '/' || c == '=' ||
             c == '!' || c == '<' || c == '>' || c == '&' || c == '|') && Peek() == c)
        {
            _position += 2;
            _column += 2;
            _tokens.Add(new Token(TokenType.Operator, _source.Substring(start, 2), _line, startCol));
            return;
        }
        // Операторы присваивания с равенством (+=, -=, *=, /=, %=, &=, |=, ^=)
        if ((c == '+' || c == '-' || c == '*' || c == '/' || c == '%' ||
             c == '&' || c == '|' || c == '^') && Peek() == '=')
        {
            _position += 2;
            _column += 2;
            _tokens.Add(new Token(TokenType.Operator, _source.Substring(start, 2), _line, startCol));
            return;
        }
        // Составные операторы сравнения: <=, >=, !=
        if ((c == '<' || c == '>' || c == '!') && Peek() == '=')
        {
            _position += 2;
            _column += 2;
            _tokens.Add(new Token(TokenType.Operator, _source.Substring(start, 2), _line, startCol));
            return;
        }

        // Обработка ->
        if (c == '-' && Peek() == '>')
        {
            _position += 2;
            _column += 2;
            _tokens.Add(new Token(TokenType.Arrow, "->", _line, startCol));
            return;
        }
        // Одиночные операторы
        _position++;
        _column++;
        _tokens.Add(new Token(TokenType.Operator, c.ToString(), _line, startCol));
    }
}
