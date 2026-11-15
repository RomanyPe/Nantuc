// ============================================================
// Parser — разбор исходника C-подобного языка.
//
// Partial-файлы по зонам ответственности:
//   - Parser.cs               — поля, Parse(), Advance(), Expect() (этот файл)
//   - Parser.TopLevel.cs      — struct / #include / extern / global / function
//   - Parser.Declarations.cs  — типы, деклараторы, переменные
//   - Parser.Statements.cs    — if/while/for/do/return/break/continue/asm
//   - Parser.Expressions.cs   — приоритетная цепочка выражений
//   - Parser.Literals.cs      — строковые и символьные литералы
// ============================================================
using Nantuc.Compiler.C.AST;
using Nantuc.Compiler.C.Lexer;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.Parser;

public partial class Parser(List<Token> tokens, ErrorReporter erRep)
{
    private readonly List<Token> _tokens = tokens;
    private readonly ErrorReporter _erRep = erRep;
    private readonly HashSet<string> _structNames = new(StringComparer.Ordinal);
    private int _position;

    private Token Current => _tokens[_position];
    public Token CurrentToken => Current;

    public ProgramNode Parse()
    {
        var program = new ProgramNode();
        while (Current.Type != TokenType.EOF)
            ParseTopLevelItem(program);
        return program;
    }

    private void Advance() => _position++;

    private Token Expect(TokenType type, string? value = null)
    {
        if (Current.Type != type)
            _erRep.Throw(ErrorCode.Parser_ExpectedToken,
                type.AsText, Current.Type.AsText, Current.Value, Current.Column);
        if (value != null && Current.Value != value)
            _erRep.Throw(ErrorCode.Parser_ExpectedToken,
                value, Current.Type.AsText, Current.Value, Current.Column);

        Token token = Current;
        Advance();
        return token;
    }
}