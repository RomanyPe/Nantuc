namespace Nantuc.Compiler.C.Lexer;

public partial class Lexer
{
    // ============================================================
    // Разбор операторов.
    //
    // Таблица упорядочена по приоритету: сначала 3-символьные,
    // затем 2-символьные в порядке оригинальной логики
    // (same-char → assign → compare → arrow), потом 1-символьные.
    //
    // ВНИМАНИЕ: оригинальный лексер трактует "**" и "!!" как единый
    // оператор — сохранено для совместимости (вероятно, баг).
    // "//" и "/*" сюда не доходят — они перехвачены в Tokenize().
    // ============================================================
    private static readonly (string Text, TokenType Type)[] Operators =
    [
        // ----- 3-символьные -----
        ("<<=", TokenType.Operator),
        (">>=", TokenType.Operator),

        // ----- 2-символьные: одинаковые -----
        ("++",  TokenType.Operator),
        ("--",  TokenType.Operator),
        ("**",  TokenType.Operator),
        ("==",  TokenType.Operator),
        ("!!",  TokenType.Operator),
        ("<<",  TokenType.Operator),
        (">>",  TokenType.Operator),
        ("&&",  TokenType.Operator),
        ("||",  TokenType.Operator),

        // ----- 2-символьные: compound assignment -----
        ("+=",  TokenType.Operator),
        ("-=",  TokenType.Operator),
        ("*=",  TokenType.Operator),
        ("/=",  TokenType.Operator),
        ("%=",  TokenType.Operator),
        ("&=",  TokenType.Operator),
        ("|=",  TokenType.Operator),
        ("^=",  TokenType.Operator),

        // ----- 2-символьные: сравнение -----
        ("<=",  TokenType.Operator),
        (">=",  TokenType.Operator),
        ("!=",  TokenType.Operator),

        // ----- 2-символьный: arrow -----
        ("->",  TokenType.Arrow),
    ];

    private void ReadOperator()
    {
        int startCol = _column;

        foreach (var (text, type) in Operators)
        {
            if (_source.AsSpan(_position).StartsWith(text))
            {
                _tokens.Add(new Token(type, text, _line, startCol));
                Advance(text.Length);
                return;
            }
        }

        // Односимвольный оператор
        char c = _source[_position];
        _tokens.Add(new Token(TokenType.Operator, c.ToString(), _line, startCol));
        Advance();
    }
}