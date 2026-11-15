namespace Nantuc.Compiler.C.Lexer;

public class Token(TokenType type, string value, int line, int column)
{
    public TokenType Type { get; } = type;
    public string Value { get; } = value;
    public int Line { get; } = line;
    public int Column { get; } = column;

    public override string ToString() => $"{Type.AsText}: '{Value}' at {Line}:{Column}";
}
