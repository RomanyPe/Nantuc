namespace Nantuc.Compiler.C.Lexer;

// ============================================================
// 1. ЛЕКСИЧЕСКИЙ АНАЛИЗАТОР (без изменений)
// ============================================================
public enum TokenType
{
    Identifier, Number, String, Char,
    Keyword, Operator, Punctuation,
    Comment, Whitespace, EOF, Arrow   // оператор ->
}
