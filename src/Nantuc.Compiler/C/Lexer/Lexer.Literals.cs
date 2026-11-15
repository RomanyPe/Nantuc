namespace Nantuc.Compiler.C.Lexer;

public partial class Lexer
{
    // ============================================================
    // Идентификаторы, числа, строковые и символьные литералы.
    // ============================================================
    private void ReadIdentifier()
    {
        int start = _position;
        int startCol = _column;

        while (_position < _source.Length
               && (char.IsLetterOrDigit(_source[_position]) || _source[_position] == '_'))
        {
            Advance();
        }

        string value = _source[start.._position];
        TokenType type = MiniCLanguageDefinition.Keywords.Contains(value)
            ? TokenType.Keyword
            : TokenType.Identifier;
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
            Advance(2);
        }

        while (_position < _source.Length)
        {
            char c = _source[_position];
            if (char.IsDigit(c) || (isHex && IsHexDigit(c)))
            {
                Advance();
            }
            else if (c == '.' && !isFloat)
            {
                isFloat = true;
                Advance();
            }
            else break;
        }

        _tokens.Add(new Token(TokenType.Number, _source[start.._position], _line, startCol));
    }

    private static bool IsHexDigit(char c)
        => (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    private void ReadString()
    {
        int start = _position;
        int startCol = _column;
        Advance();                                      // открывающая '"'

        while (_position < _source.Length && _source[_position] != '"')
        {
            if (_source[_position] == '\\') Advance(2); // escape: \" \n \\ ...
            else Advance();
        }

        Advance();                                      // закрывающая '"'
        _tokens.Add(new Token(TokenType.String, _source[start.._position], _line, startCol));
    }

    private void ReadChar()
    {
        int start = _position;
        int startCol = _column;
        Advance();                                      // открывающая '\''

        if (_source[_position] == '\\') Advance(2);     // escape
        else Advance();      // обычный символ

        Advance();                                      // закрывающая '\''
        _tokens.Add(new Token(TokenType.Char, _source[start.._position], _line, startCol));
    }
}