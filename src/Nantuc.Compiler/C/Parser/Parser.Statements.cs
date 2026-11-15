using Nantuc.Compiler.C.AST;
using System.Text;
using Nantuc.Compiler.C.Lexer;

namespace Nantuc.Compiler.C.Parser;

public partial class Parser
{
    // ============================================================
    // Statement-парсеры: блоки, if/while/for/do, return/break/continue,
    // inline asm.
    // ============================================================
    private BlockNode ParseBlock()
    {
        var block = new BlockNode();
        while (Current.Value != "}")
        {
            if (IsTypeSpecifier())
            {
                var (type, isPointer, pointedType, prefixConst, ptrConst) = ParseDeclSpec();
                string name = Expect(TokenType.Identifier).Value;
                var varNodes = ParseDeclarators(type, name, isPointer, pointedType, prefixConst, ptrConst);
                block.Statements.AddRange(varNodes);
                Expect(TokenType.Punctuation, ";");
            }
            else
            {
                block.Statements.Add(ParseBlockType(Current.Value));
            }
        }
        Expect(TokenType.Punctuation, "}");
        return block;
    }

    private BlockNode ParseBody()
    {
        if (Current.Value == "{")
        {
            Expect(TokenType.Punctuation, "{");
            return ParseBlock();
        }

        // Однострочное тело
        var block = new BlockNode();
        block.Statements.Add(ParseBlockType(Current.Value));
        return block;
    }

    private ASTNode ParseBlockType(string text) => text switch
    {
        "if" => ParseIf(),
        "while" => ParseWhile(),
        "for" => ParseFor(),
        "do" => ParseDoWhile(),
        "return" => ParseReturn(),
        "break" => ParseBreak(),
        "continue" => ParseContinue(),
        "asm" => ParseInlineAsm(),
        _ => ParseStatement(),
    };

    private IfNode ParseIf()
    {
        Expect(TokenType.Keyword, "if");
        Expect(TokenType.Punctuation, "(");
        ASTNode condition = ParseExpression();
        Expect(TokenType.Punctuation, ")");

        var thenBlock = ParseBody();

        BlockNode? elseBlock = null;
        if (Current.Value == "else")
        {
            Expect(TokenType.Keyword, "else");
            elseBlock = ParseBody();
        }

        return new IfNode(condition, thenBlock, elseBlock);
    }

    private WhileNode ParseWhile()
    {
        Expect(TokenType.Keyword, "while");
        Expect(TokenType.Punctuation, "(");
        ASTNode condition = ParseExpression();
        Expect(TokenType.Punctuation, ")");

        var body = ParseBody();
        return new WhileNode(condition, body);
    }

    private ForNode ParseFor()
    {
        Expect(TokenType.Keyword, "for");
        Expect(TokenType.Punctuation, "(");

        ASTNode? init = null;
        if (Current.Value != ";")
        {
            if (IsTypeSpecifier())
            {
                var (type, isPointer, pointedType, prefixConst, ptrConst) = ParseDeclSpec();
                string name = Expect(TokenType.Identifier).Value;
                var varNodes = ParseDeclarators(type, name, isPointer, pointedType, prefixConst, ptrConst);

                if (varNodes.Count == 1)
                {
                    init = varNodes[0];
                }
                else
                {
                    var initBlock = new BlockNode();
                    initBlock.Statements.AddRange(varNodes);
                    init = initBlock;
                }
            }
            else
            {
                init = ParseExpression();
            }
        }
        Expect(TokenType.Punctuation, ";");

        ASTNode? condition = null;
        if (Current.Value != ";") condition = ParseExpression();
        Expect(TokenType.Punctuation, ";");

        ASTNode? increment = null;
        if (Current.Value != ")") increment = ParseExpression();
        Expect(TokenType.Punctuation, ")");

        var body = ParseBody();
        return new ForNode(init, condition, increment, body);
    }

    private DoWhileNode ParseDoWhile()
    {
        Expect(TokenType.Keyword, "do");
        var body = ParseBody();

        Expect(TokenType.Keyword, "while");
        Expect(TokenType.Punctuation, "(");
        ASTNode cond = ParseExpression();
        Expect(TokenType.Punctuation, ")");
        Expect(TokenType.Punctuation, ";");

        return new DoWhileNode(body, cond);
    }

    private ReturnNode ParseReturn()
    {
        Expect(TokenType.Keyword, "return");
        ASTNode? value = null;
        if (Current.Value != ";") value = ParseExpression();
        Expect(TokenType.Punctuation, ";");
        return new ReturnNode(value);
    }

    private BreakNode ParseBreak()
    {
        Expect(TokenType.Keyword, "break");
        Expect(TokenType.Punctuation, ";");
        return new BreakNode();
    }

    private ContinueNode ParseContinue()
    {
        Expect(TokenType.Keyword, "continue");
        Expect(TokenType.Punctuation, ";");
        return new ContinueNode();
    }

    private ASTNode ParseStatement()
    {
        ASTNode expr = ParseExpression();
        Expect(TokenType.Punctuation, ";");
        return expr;
    }

    private InlineAsmNode ParseInlineAsm()
    {
        Expect(TokenType.Keyword, "asm");
        Expect(TokenType.Punctuation, "{");

        var sb = new StringBuilder();
        int braceDepth = 1;
        while (braceDepth > 0)
        {
            if (Current.Type == TokenType.Punctuation && Current.Value == "{")
                braceDepth++;
            else if (Current.Type == TokenType.Punctuation && Current.Value == "}")
            {
                braceDepth--;
                if (braceDepth == 0) break;
            }

            sb.Append(Current.Value);
            sb.Append(Current.Value == ";" ? '\n' : ' ');
            Advance();
        }

        Expect(TokenType.Punctuation, "}");
        return new InlineAsmNode(sb.ToString().Trim());
    }
}