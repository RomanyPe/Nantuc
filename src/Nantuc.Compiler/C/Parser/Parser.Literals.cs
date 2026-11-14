using Nantuc.Compiler.C.AST;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.Parser;

public partial class Parser
{
    // ============================================================
    // Литералы: строки и символы (с escape-последовательностями).
    // ============================================================
    private List<ASTNode> ParseStringToNodes(string raw)
    {
        string inner = raw[1..^1];
        var nodes = new List<ASTNode>(inner.Length + 1);

        for (int i = 0; i < inner.Length; i++)
        {
            char c = inner[i];
            if (c != '\\')
            {
                nodes.Add(new NumberNode(c));
                continue;
            }

            i++;
            if (i >= inner.Length)
                _erRep.Throw(ErrorCode.Parser_InvalidCharLiteral, raw);

            char esc = inner[i];
            switch (esc)
            {
                case 'n': nodes.Add(new NumberNode('\n')); break;
                case 't': nodes.Add(new NumberNode('\t')); break;
                case 'r': nodes.Add(new NumberNode('\r')); break;
                case '0': nodes.Add(new NumberNode('\0')); break;
                case '\\': nodes.Add(new NumberNode('\\')); break;
                case '\'': nodes.Add(new NumberNode('\'')); break;
                case '"': nodes.Add(new NumberNode('"')); break;
                case 'x':
                    if (i + 2 >= inner.Length)
                        _erRep.Throw(ErrorCode.Parser_InvalidCharLiteral, raw);
                    nodes.Add(new NumberNode(Convert.ToInt64(inner.Substring(i + 1, 2), 16)));
                    i += 2;
                    break;
                default:
                    _erRep.Throw(ErrorCode.Parser_InvalidCharLiteral, raw);
                    break;
            }
        }

        nodes.Add(new NumberNode('\0'));   // NUL-терминатор
        return nodes;
    }

    private int ParseCharLiteral(string text)
    {
        if (string.IsNullOrEmpty(text))
            _erRep.Throw(ErrorCode.Parser_InvalidCharLiteral, text);

        if (text[0] != '\\')
        {
            if (text.Length != 1)
                _erRep.Throw(ErrorCode.Parser_InvalidCharLiteral, text);
            return text[0];
        }

        if (text.Length < 2)
            _erRep.Throw(ErrorCode.Parser_InvalidCharLiteral, text);

        char escape = text[1];
        return escape switch
        {
            'n' => '\n',
            't' => '\t',
            'r' => '\r',
            '0' => '\0',
            '\\' => '\\',
            '\'' => '\'',
            '"' => '"',
            'x' => ParseHexChar(text),
            _ => _erRep.Throw<int>(ErrorCode.Parser_InvalidCharLiteral, text),
        };
    }

    private int ParseHexChar(string text)
    {
        if (text.Length != 4)
            _erRep.Throw(ErrorCode.Parser_InvalidCharLiteral, text);
        return Convert.ToInt32(text.Substring(2, 2), 16);
    }
}