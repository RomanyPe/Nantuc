namespace Nantuc.Compiler.C;

public static class TokenTypeExtension
{
    extension(TokenType t)
    {
        public string AsText => t switch { 
            TokenType.Identifier => "Identifier", 
            TokenType.Number => "Number", 
            TokenType.String => "String", 
            TokenType.Char => "Char", 
            TokenType.Keyword => "Keyword", 
            TokenType.Operator => "Operator",
            TokenType.Punctuation => "Punctuation", 
            TokenType.Comment => "Comment", 
            TokenType.Whitespace => "Whitespace", 
            TokenType.EOF => "EOF", 
            TokenType.Arrow => "Arrow",
            _ => $"Unknown TokenType ({(int)t})"
        };
    }
}
