using Nantuc.Compiler.C.AST;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.Parser;

public partial class Parser
{
    // ============================================================
    // Приоритетная цепочка выражений (C-приоритеты).
    //
    // ParseComma
    //   └ ParseAssignment
    //     └ ParseLogicalOr
    //       └ ParseLogicalAnd
    //         └ ParseBitwiseOr
    //           └ ParseBitwiseXor
    //             └ ParseBitwiseAnd
    //               └ ParseEquality
    //                 └ ParseRelational
    //                   └ ParseShift
    //                     └ ParseAdditive
    //                       └ ParseMultiplicative
    //                         └ ParseUnary
    //                           └ ParsePrimary
    // ============================================================
    private ASTNode ParseExpression() => ParseComma();

    private ASTNode ParseComma()
    {
        ASTNode left = ParseAssignment();
        while (Current.Type == TokenType.Punctuation && Current.Value == ",")
        {
            Advance();
            left = new BinaryOpNode(",", left, ParseAssignment());
        }
        return left;
    }

    private ASTNode ParseAssignment()
    {
        ASTNode left = ParseLogicalOr();
        if (Current.Type == TokenType.Operator &&
            Current.Value is "=" or "+=" or "-=" or "*=" or "/=" or "%=" or
                            "&=" or "|=" or "^=" or "<<=" or ">>=")
        {
            string op = Current.Value;
            Advance();
            ASTNode right = ParseAssignment();
            return left switch
            {
                IdentifierNode id => new AssignmentNode(id.Name, right) { LValue = left, CompoundOp = op },
                ArrayAccessNode arr => new AssignmentNode(arr.ArrayName, right, arr.Index) { LValue = left, CompoundOp = op },
                MemberAccessNode => new AssignmentNode(null!, right) { LValue = left, CompoundOp = op },
                DereferenceNode => new AssignmentNode(null!, right) { LValue = left, CompoundOp = op },
                _ => _erRep.Throw<ASTNode>(ErrorCode.Parser_InvalidAssignment),
            };
        }
        return left;
    }

    private ASTNode ParseLogicalOr()
    {
        ASTNode left = ParseLogicalAnd();
        while (Current.Type == TokenType.Operator && Current.Value == "||")
        {
            string op = Current.Value;
            Advance();
            left = new BinaryOpNode(op, left, ParseLogicalAnd());
        }
        return left;
    }

    private ASTNode ParseLogicalAnd()
    {
        ASTNode left = ParseBitwiseOr();
        while (Current.Type == TokenType.Operator && Current.Value == "&&")
        {
            string op = Current.Value;
            Advance();
            left = new BinaryOpNode(op, left, ParseBitwiseOr());
        }
        return left;
    }

    private ASTNode ParseBitwiseOr()
    {
        ASTNode left = ParseBitwiseXor();
        while (Current.Type == TokenType.Operator && Current.Value == "|")
        {
            string op = Current.Value;
            Advance();
            left = new BinaryOpNode(op, left, ParseBitwiseXor());
        }
        return left;
    }

    private ASTNode ParseBitwiseXor()
    {
        ASTNode left = ParseBitwiseAnd();
        while (Current.Type == TokenType.Operator && Current.Value == "^")
        {
            string op = Current.Value;
            Advance();
            left = new BinaryOpNode(op, left, ParseBitwiseAnd());
        }
        return left;
    }

    private ASTNode ParseBitwiseAnd()
    {
        ASTNode left = ParseEquality();
        while (Current.Type == TokenType.Operator && Current.Value == "&")
        {
            string op = Current.Value;
            Advance();
            left = new BinaryOpNode(op, left, ParseEquality());
        }
        return left;
    }

    private ASTNode ParseEquality()
    {
        ASTNode left = ParseRelational();
        while (Current.Type == TokenType.Operator && (Current.Value == "==" || Current.Value == "!="))
        {
            string op = Current.Value;
            Advance();
            left = new BinaryOpNode(op, left, ParseRelational());
        }
        return left;
    }

    private ASTNode ParseRelational()
    {
        ASTNode left = ParseShift();
        while (Current.Type == TokenType.Operator &&
               Current.Value is "<" or ">" or "<=" or ">=")
        {
            string op = Current.Value;
            Advance();
            left = new BinaryOpNode(op, left, ParseShift());
        }
        return left;
    }

    private ASTNode ParseShift()
    {
        ASTNode left = ParseAdditive();
        while (Current.Type == TokenType.Operator && (Current.Value == "<<" || Current.Value == ">>"))
        {
            string op = Current.Value;
            Advance();
            left = new BinaryOpNode(op, left, ParseAdditive());
        }
        return left;
    }

    private ASTNode ParseAdditive()
    {
        ASTNode left = ParseMultiplicative();
        while (Current.Type == TokenType.Operator && (Current.Value == "+" || Current.Value == "-"))
        {
            string op = Current.Value;
            Advance();
            left = new BinaryOpNode(op, left, ParseMultiplicative());
        }
        return left;
    }

    private ASTNode ParseMultiplicative()
    {
        ASTNode left = ParseUnary();
        while (Current.Type == TokenType.Operator &&
               (Current.Value == "*" || Current.Value == "/" || Current.Value == "%"))
        {
            string op = Current.Value;
            Advance();
            left = new BinaryOpNode(op, left, ParseUnary());
        }
        return left;
    }

    private ASTNode ParseUnary()
    {
        if (Current.Type == TokenType.Keyword && Current.Value == "sizeof")
        {
            Advance();
            if (Current.Type == TokenType.Punctuation && Current.Value == "(" && IsSizeofTypeParen())
                return ParseSizeofTypeInParens();
            return new SizeofExprNode(ParseUnary());
        }

        // (type) без sizeof — сохранено как есть (см. примечание в ответе)
        if (Current.Type == TokenType.Punctuation && Current.Value == "(" && IsSizeofTypeParen())
            return ParseSizeofTypeInParens();

        if (Current.Type == TokenType.Operator && (Current.Value == "++" || Current.Value == "--"))
        {
            string op = Current.Value;
            Advance();
            ASTNode operand = ParseUnary();
            return new IncrementNode(operand, op == "++" ? 1 : -1, isPrefix: true);
        }

        if (Current.Type == TokenType.Operator &&
            Current.Value is "+" or "-" or "!" or "~" or "*" or "&")
        {
            string op = Current.Value;
            Advance();
            ASTNode operand = ParseUnary();
            return op switch
            {
                "*" => new DereferenceNode(operand),
                "&" => new AddressOfNode(operand),
                _ => new UnaryOpNode(op, operand),
            };
        }

        return ParsePrimary();
    }

    /// <summary>Разбор (тип) после '('. Поведение — как в исходной версии.</summary>
    private SizeofTypeNode ParseSizeofTypeInParens()
    {
        Expect(TokenType.Punctuation, "(");
        if (Current.Type == TokenType.Keyword && Current.Value == "const") Advance();
        string typeName = ParseType();
        if (Current.Type == TokenType.Keyword && Current.Value == "const") Advance();
        bool isPointer = false;
        if (Current.Type == TokenType.Operator && Current.Value == "*") { isPointer = true; Advance(); }
        Expect(TokenType.Punctuation, ")");
        return new SizeofTypeNode(typeName, isPointer);
    }

    private bool IsSizeofTypeParen()
    {
        int save = _position;
        Advance();
        bool isType = IsTypeSpecifier();
        _position = save;
        return isType;
    }

    private ASTNode ParsePrimary()
    {
        ASTNode expr = ParsePrimaryAtom();
        expr = ParsePostfixMemberAndIncrement(expr);
        expr = ParsePostfixIndirectCall(expr);
        return expr;
    }

    private ASTNode ParsePrimaryAtom()
    {
        if (Current.Type == TokenType.Number)
            return ParseNumberAtom();

        if (Current.Type == TokenType.Identifier)
            return ParseIdentifierAtom();

        if (Current.Value == "(")
        {
            Advance();
            ASTNode inner = ParseExpression();
            Expect(TokenType.Punctuation, ")");
            return inner;
        }

        if (Current.Type == TokenType.Char)
            return ParseCharAtom();

        _erRep.Throw(ErrorCode.Parser_UnexpectedToken,
            Current.Type.AsText, Current.Value, Current.Column);
        return null!;
    }

    private NumberNode ParseNumberAtom()
    {
        string numStr = Current.Value;
        long value = numStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToInt64(numStr, 16)
            : long.Parse(numStr);
        Advance();
        return new NumberNode(value);
    }

    private ASTNode ParseIdentifierAtom()
    {
        string name = Current.Value;
        Advance();

        if (Current.Type == TokenType.Punctuation && Current.Value == "[")
        {
            // a[i]
            Expect(TokenType.Punctuation, "[");
            ASTNode index = ParseExpression();
            Expect(TokenType.Punctuation, "]");
            return new ArrayAccessNode(name, index);
        }

        if (Current.Type == TokenType.Punctuation && Current.Value == "(")
        {
            // f(args)
            var call = new FunctionCallNode(name);
            Expect(TokenType.Punctuation, "(");
            if (Current.Value != ")")
            {
                while (true)
                {
                    call.Arguments.Add(ParseAssignment());
                    if (Current.Value != ",") break;
                    Expect(TokenType.Punctuation, ",");
                }
            }
            Expect(TokenType.Punctuation, ")");
            return call;
        }

        return new IdentifierNode(name);
    }

    private NumberNode ParseCharAtom()
    {
        string tokenValue = Current.Value;      // 'a' или '\n'
        string inner = tokenValue[1..^1];
        int code = ParseCharLiteral(inner);
        Advance();
        return new NumberNode(code);
    }

    private ASTNode ParsePostfixMemberAndIncrement(ASTNode expr)
    {
        while (true)
        {
            if ((Current.Type == TokenType.Punctuation && Current.Value == ".")
                || Current.Type == TokenType.Arrow)
            {
                bool isArrow = Current.Type == TokenType.Arrow;
                Advance();
                string fieldName = Expect(TokenType.Identifier).Value;
                expr = new MemberAccessNode(expr, fieldName, isArrow);
                continue;
            }

            if (Current.Type == TokenType.Operator && (Current.Value == "++" || Current.Value == "--"))
            {
                string op = Current.Value;
                Advance();
                expr = new IncrementNode(expr, op == "++" ? 1 : -1, isPrefix: false);
                continue;
            }

            return expr;
        }
    }

    private ASTNode ParsePostfixIndirectCall(ASTNode expr)
    {
        while (Current.Type == TokenType.Punctuation && Current.Value == "(")
        {
            Advance();
            var args = new List<ASTNode>();
            if (Current.Value != ")")
            {
                while (true)
                {
                    args.Add(ParseAssignment());
                    if (Current.Value != ",") break;
                    Advance();
                }
            }
            Expect(TokenType.Punctuation, ")");
            expr = new IndirectCallNode(expr, args);
        }
        return expr;
    }
}