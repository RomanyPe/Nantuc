using Nantuc.Kernel.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Nantuc.Compiler.C;

public class Parser(List<Token> tokens)
{
    private readonly List<Token> _tokens = tokens;
    private readonly HashSet<string> _structNames = new(StringComparer.Ordinal);
    private int _position;
    private Token Current => _tokens[_position];
    public Token CurrentToken => Current;

    public ProgramNode Parse()
    {
        var program = new ProgramNode();
        while (Current.Type != TokenType.EOF)
        {
            if (Current.Type == TokenType.Keyword && Current.Value == "struct")
            {
                bool isDecl = _position + 2 < _tokens.Count
                              && _tokens[_position + 2].Type == TokenType.Punctuation
                              && _tokens[_position + 2].Value == "{";

                if (isDecl)
                {
                    Advance();                                         // struct
                    string name = Expect(TokenType.Identifier).Value;  // Point
                    _structNames.Add(name);

                    Expect(TokenType.Punctuation, "{");
                    var fields = new List<(string, string)>();
                    while (Current.Value != "}")
                    {
                        string ftype = ParseType();
                        string fname = Expect(TokenType.Identifier).Value;
                        fields.Add((ftype, fname));
                        Expect(TokenType.Punctuation, ";");
                    }
                    Expect(TokenType.Punctuation, "}");
                    if (Current.Type == TokenType.Punctuation && Current.Value == ";")
                        Advance();
                    program.StructNodes.Add(new StructDeclNode(name, fields));
                    continue;
                }
            }

            if (Current.Type == TokenType.Operator && Current.Value == "#")
            {
                Expect(TokenType.Operator, "#");
                string directive = Expect(TokenType.Identifier).Value; // "include"
                if (directive != "include")
                    ThrowCompiler.Throw(ErrorCode.Parser_UnknownDirective, directive);
                string filePath = Expect(TokenType.String).Value.Trim('"');
                program.IncludesList.Add(filePath);
                // точка с запятой не требуется
                continue;
            }

            if (Current.Type == TokenType.Keyword && Current.Value == "extern")
            {
                Advance(); // съедаем extern
                string type = ParseType(); // тип возврата или тип переменной

                if (Current.Value == "*")
                {
                    Advance();
                }

                string name = Expect(TokenType.Identifier).Value;

                if (Current.Value == "(") // функция
                {
                    Expect(TokenType.Punctuation, "(");
                    var parameters = new List<ParameterNode>();
                    if (Current.Value != ")")
                    {
                        while (true)
                        {
                            string paramType = ParseType();
                            bool paramIsPtr = false;
                            if (Current.Value == "*")
                            {
                                paramIsPtr = true;
                                Advance();
                            }
                            string paramName = Expect(TokenType.Identifier).Value;
                            parameters.Add(new ParameterNode(paramType, paramName)
                            {
                                IsPointer = paramIsPtr,
                                PointedType = paramType
                            });
                            if (Current.Value != ",") break;
                            Expect(TokenType.Punctuation, ",");
                        }
                    }
                    Expect(TokenType.Punctuation, ")");
                    Expect(TokenType.Punctuation, ";");

                    var extFunc = new FunctionNode(name, type, null!)
                    {
                        IsExternal = true,
                        Parameters = parameters
                    };
                    program.FunctionNodes.Add(extFunc);
                }
                else // переменная (пока не обрабатываем, но можно пропустить)
                {
                    // Ожидаем ';'
                    while (Current.Value != ";") Advance();
                    Advance(); // пропускаем ';'
                }
                continue; // переходим к следующему токену
            }

            if (IsTypeSpecifier())
            {
                string type = ParseType();

                bool isPointer = false;
                string pointedType = null!;

                if (Current.Value == "*")
                {
                    isPointer = true;
                    pointedType = type; // указатель на данный тип
                    Advance(); // съедаем '*'
                }

                string name = Expect(TokenType.Identifier).Value;

                if (Current.Type == TokenType.Punctuation && Current.Value == "(")
                {
                    var func = ParseFunction(type, name);
                    program.FunctionNodes.Add(func);
                }
                else
                {
                    var varNodes = ParseDeclarators(type, name, isPointer, pointedType);
                    program.GlobalVarNodes.AddRange(varNodes);
                    Expect(TokenType.Punctuation, ";");
                }
            }
            else
            {
                ThrowCompiler.Throw(ErrorCode.Parser_UnexpectedToken, Current.Type, Current.Value, Current.Column);

            }
        }
        return program;
    }

    private bool IsTypeSpecifier()
    {
        if (Current.Type == TokenType.Keyword &&
            (Current.Value == "int"
            || Current.Value == "char"
            || Current.Value == "void"
            || Current.Value == "byte"
            || Current.Value == "ushort"
            || Current.Value == "ulong"
            || Current.Value == "short"
            || Current.Value == "long"
            || Current.Value == "uint"))
            return true;

        if (Current.Type == TokenType.Identifier && _structNames.Contains(Current.Value))
            return true;

        // Разрешаем "struct TypeName"
        if (Current.Type == TokenType.Keyword && Current.Value == "struct")
            return true;

        return false;
    }
    private string ParseType()
    {
        if (Current.Type == TokenType.Keyword && Current.Value == "struct")
        {
            Advance();
            string name = Expect(TokenType.Identifier).Value;
            if (!_structNames.Contains(name))
                ThrowCompiler.Throw(ErrorCode.CodeGen_UnknownStructType, name);
            return name;
        }
        if (Current.Type == TokenType.Identifier && _structNames.Contains(Current.Value))
            return Expect(TokenType.Identifier).Value;
        return Expect(TokenType.Keyword).Value;
    }
    // EOF
    private FunctionNode ParseFunction(string returnType, string name)
    {
        Expect(TokenType.Punctuation, "(");
        var parameters = new List<ParameterNode>();
        if (Current.Value != ")")
        {
            while (true)
            {
                string paramType = ParseType();
                bool isPointer = false;
                if (Current.Value == "*")
                {
                    isPointer = true;
                    Advance(); // съедаем '*'
                }
                string paramName = Expect(TokenType.Identifier).Value;
                // Сохраняем информацию о том, что параметр — указатель, в ParameterNode
                // Для этого потребуется расширить ParameterNode (см. ниже)
                parameters.Add(new ParameterNode(paramType, paramName)
                {
                    IsPointer = isPointer,
                    PointedType = paramType
                });
                if (Current.Value != ",") break;
                Expect(TokenType.Punctuation, ",");
            }
        }
        Expect(TokenType.Punctuation, ")");
        Expect(TokenType.Punctuation, "{");
        var body = ParseBlock();
        var func = new FunctionNode(name, returnType, body)
        {
            Parameters = parameters // Ключевая строка!
        };
        return func;
    }

    private BlockNode ParseBlock()
    {
        BlockNode block = new();
        while (Current.Value != "}")
        {
            if (IsTypeSpecifier())
            {
                string type = ParseType();

                bool isPointer = false;
                string? pointedType = null;
                if (Current.Value == "*")
                {
                    isPointer = true;
                    pointedType = type;
                    Advance();
                }

                string name = Expect(TokenType.Identifier).Value;
                var varNodes = ParseDeclarators(type, name, isPointer, pointedType);
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


    private ASTNode ParseExpression() => ParseComma();

    private ASTNode ParseComma()
    {
        ASTNode left = ParseAssignment();
        while (Current.Type == TokenType.Punctuation && Current.Value == ",")
        {
            Advance();
            ASTNode right = ParseAssignment();
            left = new BinaryOpNode(",", left, right);
        }
        return left;
    }

    private VariableNode ParseVariableTail(string type, string name, bool isPointer, string? pointedType)
    {
        // --- Массив? ---
        if (Current.Type == TokenType.Punctuation && Current.Value == "[")
        {
            Expect(TokenType.Punctuation, "[");
            int size = -1;
            if (Current.Type == TokenType.Number)
            {
                size = int.Parse(Current.Value);
                Advance();
            }
            Expect(TokenType.Punctuation, "]");

            var varNode = new VariableNode(type, name)
            {
                IsArray = true,
                ArraySize = size < 0 ? 0 : size,
                IsPointer = isPointer,
                PointedType = pointedType
            };

            // Инициализатор { ... }?
            if (Current.Type == TokenType.Operator && Current.Value == "=")
            {
                Expect(TokenType.Operator, "=");
                if (type == "char" && Current.Type == TokenType.String)
                {
                    var initList = ParseStringToNodes(Current.Value);
                    Advance();
                    varNode.ArrayInitializer = initList;
                    if (size < 0)
                    {
                        varNode.ArraySize = initList.Count;
                    }
                    else if (initList.Count > size)
                    {
                        ThrowCompiler.Throw(ErrorCode.Parser_InvalidAssignment, name);
                    }
                    return varNode;
                }
                Expect(TokenType.Punctuation, "{");

                List<ASTNode> list = [];
                if (Current.Value != "}")
                {
                    while (true)
                    {
                        list.Add(ParseAssignment());             // было ParseExpression()
                        if (Current.Value != ",") break;
                        Expect(TokenType.Punctuation, ",");
                    }
                }
                Expect(TokenType.Punctuation, "}");

                varNode.ArrayInitializer = list;

                if (size < 0)
                    varNode.ArraySize = list.Count;          // int arr[] = {...}
                else if (list.Count > size)
                    ThrowCompiler.Throw(ErrorCode.Parser_InvalidAssignment, name); // слишком много инициализаторов
                
            }

            else if (size < 0)
            {
                // int arr[]; без инициализатора — размер неизвестен
                ThrowCompiler.Throw(ErrorCode.Parser_ArraySizeNotConstant, name);
            }

            return varNode;
        }

        // --- Скаляр ---
        ASTNode? initializer = null;
        if (Current.Type == TokenType.Operator && Current.Value == "=")
        {
            Expect(TokenType.Operator, "=");
            initializer = ParseAssignment();
        }
        return new VariableNode(type, name, initializer)
        {
            IsPointer = isPointer,
            PointedType = pointedType
        };
    }

    private ASTNode ParseBlockType(string text) => text switch
    {
        "if" => ParseIf(),
        "while" => ParseWhile(),
        "for" => ParseFor(),
        "return" => ParseReturn(),
        "asm" => ParseInlineAsm(),
        "break" => ParseBreak(),
        "continue" => ParseContinue(),
        "do" => ParseDoWhile(),
        _ => ParseStatement(),
    };

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

    private static List<ASTNode> ParseStringToNodes(string raw)
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
                ThrowCompiler.Throw(ErrorCode.Parser_InvalidCharLiteral, raw);

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
                        ThrowCompiler.Throw(ErrorCode.Parser_InvalidCharLiteral, raw);
                    nodes.Add(new NumberNode(Convert.ToInt64(inner.Substring(i + 1, 2), 16)));
                    i += 2;
                    break;
                default:
                    ThrowCompiler.Throw(ErrorCode.Parser_InvalidCharLiteral, raw);
                    break;

            }
        }
        nodes.Add(new NumberNode('\0'));
        return nodes;
    }

    private BlockNode ParseBody()
    {
        if (Current.Value == "{")
        {
            Expect(TokenType.Punctuation, "{");
            return ParseBlock();       // ParseBlock ожидает "{" уже съеденным
        }

        // Однострочное тело: if/while/for/return/asm/expression statement
        var block = new BlockNode();
        block.Statements.Add(ParseBlockType(Current.Value));
        return block;
    }

    private IfNode ParseIf()
    {
        Expect(TokenType.Keyword, "if");
        Expect(TokenType.Punctuation, "(");
        ASTNode condition = ParseExpression();
        Expect(TokenType.Punctuation, ")");

        var thenBlock = ParseBody();          // ← было: Expect "{" + ParseBlock()

        BlockNode? elseBlock = null;
        if (Current.Value == "else")
        {
            Expect(TokenType.Keyword, "else");
            elseBlock = ParseBody();          // ← было: Expect "{" + ParseBlock()
        }
        return new IfNode(condition, thenBlock, elseBlock);
    }
    private WhileNode ParseWhile()
    {
        Expect(TokenType.Keyword, "while");
        Expect(TokenType.Punctuation, "(");
        ASTNode condition = ParseExpression();
        Expect(TokenType.Punctuation, ")");

        var body = ParseBody();               // ← было: Expect "{" + ParseBlock()
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
                string type = ParseType();
                bool isPointer = false; string? pointedType = null;
                if (Current.Value == "*") { isPointer = true; pointedType = type; Advance(); }

                string name = Expect(TokenType.Identifier).Value;
                var varNodes = ParseDeclarators(type, name, isPointer, pointedType);

                if (varNodes.Count == 1)
                    init = varNodes[0];
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
        if (Current.Value != ";")
            condition = ParseExpression();
        Expect(TokenType.Punctuation, ";");

        ASTNode? increment = null;
        if (Current.Value != ")")
            increment = ParseExpression();
        Expect(TokenType.Punctuation, ")");

        var body = ParseBody();               // ← было: Expect "{" + ParseBlock()
        return new ForNode(init, condition, increment, body);
    }

    private List<VariableNode> ParseDeclarators(string type, string firstName,
                                            bool firstIsPointer, string? firstPointedType)
    {
        var result = new List<VariableNode> { ParseVariableTail(type, firstName, firstIsPointer, firstPointedType) };

        while (Current.Type == TokenType.Punctuation && Current.Value == ",")
        {
            Advance();  // ","
            bool isPtr = false;
            string? pointed = null;
            if (Current.Type == TokenType.Operator && Current.Value == "*")
            {
                isPtr = true;
                pointed = type;   // указатель на этот же тип
                Advance();
            }
            string name = Expect(TokenType.Identifier).Value;
            result.Add(ParseVariableTail(type, name, isPtr, pointed));
        }
        return result;
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

            // Добавляем токен к строке asm-кода
            sb.Append(Current.Value);
            switch (Current.Value)
            {
                case ";":
                    sb.Append('\n');   // новая строка после разделителя
                    break;
                default:
                    sb.Append(' ');
                    break;
            }
            Advance();
        }

        Expect(TokenType.Punctuation, "}");

        string asmCode = sb.ToString().Trim();
        return new InlineAsmNode(asmCode);
    }

    private ReturnNode ParseReturn()
    {
        Expect(TokenType.Keyword, "return");
        ASTNode? value = null;
        if (Current.Value != ";") value = ParseExpression();
        Expect(TokenType.Punctuation, ";");
        return new ReturnNode(value);
    }

    private ASTNode ParseStatement()
    {
        ASTNode expr = ParseExpression();
        Expect(TokenType.Punctuation, ";");
        return expr;
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
                _ => ThrowCompiler.Throw<ASTNode>(ErrorCode.Parser_InvalidAssignment),
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
            ASTNode right = ParseLogicalAnd();
            left = new BinaryOpNode(op, left, right);
        }
        return left;
    }
    private ASTNode ParseLogicalAnd()
    {
        // БЫЛО: ASTNode left = ParseEquality();
        ASTNode left = ParseBitwiseOr();
        while (Current.Type == TokenType.Operator && Current.Value == "&&")
        {
            string op = Current.Value;
            Advance();
            ASTNode right = ParseBitwiseOr(); // БЫЛО: ParseEquality()
            left = new BinaryOpNode(op, left, right);
        }
        return left;
    }

    // НОВЫЕ МЕТОДЫ (вставьте перед ParseEquality)
    private ASTNode ParseBitwiseOr()
    {
        ASTNode left = ParseBitwiseXor();
        while (Current.Type == TokenType.Operator && Current.Value == "|")
        {
            string op = Current.Value;
            Advance();
            ASTNode right = ParseBitwiseXor();
            left = new BinaryOpNode(op, left, right);
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
            ASTNode right = ParseBitwiseAnd();
            left = new BinaryOpNode(op, left, right);
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
            ASTNode right = ParseEquality();
            left = new BinaryOpNode(op, left, right);
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
            ASTNode right = ParseRelational();
            left = new BinaryOpNode(op, left, right);
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
            ASTNode right = ParseAdditive();
            left = new BinaryOpNode(op, left, right);
        }
        return left;
    }
    private ASTNode ParseRelational()
    {
        ASTNode left = ParseShift();
        while (Current.Type == TokenType.Operator && (Current.Value == "<" || Current.Value == ">" ||
                                                     Current.Value == "<=" || Current.Value == ">="))
        {
            string op = Current.Value;
            Advance();
            ASTNode right = ParseAdditive();
            left = new BinaryOpNode(op, left, right);
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
            ASTNode right = ParseMultiplicative();
            left = new BinaryOpNode(op, left, right);
        }
        return left;
    }

    private ASTNode ParseMultiplicative()
    {
        ASTNode left = ParseUnary();
        while (Current.Type == TokenType.Operator && (Current.Value == "*" || Current.Value == "/" || Current.Value == "%"))
        {
            string op = Current.Value;
            Advance();
            ASTNode right = ParseUnary();
            left = new BinaryOpNode(op, left, right);
        }
        return left;
    }

    private ASTNode ParseUnary()
    {
        if (Current.Type == TokenType.Keyword && Current.Value == "sizeof")
        {
            Advance();
            if (Current.Type == TokenType.Punctuation && Current.Value == "(" && IsSizeofTypeParen())
            {
                Advance();                              // "("
                string typeName = ParseType();
                bool isPointer = false;
                if (Current.Type == TokenType.Operator && Current.Value == "*") { isPointer = true; Advance(); }
                Expect(TokenType.Punctuation, ")");
                return new SizeofTypeNode(typeName, isPointer);
            }
            return new SizeofExprNode(ParseUnary());
        }
        if (Current.Type == TokenType.Operator && (Current.Value == "++" || Current.Value == "--"))
        {
            string op = Current.Value;
            Advance();
            ASTNode operand = ParseUnary();   // поддержка --++x
            return new IncrementNode(operand, op == "++" ? 1 : -1, isPrefix: true);
        }
        if (Current.Type == TokenType.Operator && (Current.Value == "+" || Current.Value == "-" ||
                                                   Current.Value == "!" || Current.Value == "~" ||
                                                   Current.Value == "*" || Current.Value == "&"))
        {
            string op = Current.Value;
            Advance();
            ASTNode operand = ParseUnary();
            if (op == "*") return new DereferenceNode(operand);
            if (op == "&") return new AddressOfNode(operand);
            return new UnaryOpNode(op, operand);
        }
        return ParsePrimary();
    }

    private bool IsSizeofTypeParen()
    {
        int save = _position;
        Advance();                              // '('
        bool isType = IsTypeSpecifier();
        _position = save;
        return isType;
    }

    private ASTNode ParsePrimary()
    {
        ASTNode expr;

        // --- Базовые первичные выражения ---
        if (Current.Type == TokenType.Number)
        {
            string numStr = Current.Value;
            long value = numStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToInt64(numStr, 16) 
                : long.Parse(numStr);

            Advance();
            expr = new NumberNode(value);
        }
        else if (Current.Type == TokenType.Identifier)
        {
            string name = Current.Value;
            Advance();
            if (Current.Type == TokenType.Punctuation && Current.Value == "[")
            {
                // доступ к массиву a[i]
                Expect(TokenType.Punctuation, "[");
                ASTNode index = ParseExpression();
                Expect(TokenType.Punctuation, "]");
                expr = new ArrayAccessNode(name, index);
            }
            else if (Current.Type == TokenType.Punctuation && Current.Value == "(")
            {
                // вызов функции
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
                expr = call;
            }
            else
            {
                expr = new IdentifierNode(name);
            }
        }
        else if (Current.Value == "(")
        {
            Advance();
            expr = ParseExpression();
            Expect(TokenType.Punctuation, ")");
        }
        else if (Current.Type == TokenType.Char)
        {
            string tokenValue = Current.Value;   // например "'a'" или "'\\n'"
                                                 // Убираем одинарные кавычки
            string inner = tokenValue[1..^1];
            int code = ParseCharLiteral(inner);
            Advance();
            expr = new NumberNode(code);
        }
        else
        {
            ThrowCompiler.Throw(ErrorCode.Parser_UnexpectedToken, Current.Type, Current.Value, Current.Column);
            return null!;
        }

        // --- ЦИКЛ ПОСТФИКСНЫХ ОПЕРАТОРОВ: . и -> ---
        while (true)
        {
            if (Current.Type == TokenType.Punctuation && Current.Value == "." || Current.Type == TokenType.Arrow)
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
            break;
        }

        return expr;
    }
    private Token Expect(TokenType type, string? value = null)
    {
        if (Current.Type != type)
            ThrowCompiler.Throw(ErrorCode.Parser_ExpectedToken, type, Current.Type, Current.Value, Current.Column);
        if (value != null && Current.Value != value)
            ThrowCompiler.Throw(ErrorCode.Parser_ExpectedToken, value, Current.Type, Current.Value, Current.Column);
        Token token = Current;
        Advance();
        
        return token;
    }

    private static int ParseCharLiteral(string text)
    {
        if (string.IsNullOrEmpty(text))
            ThrowCompiler.Throw(ErrorCode.Parser_InvalidCharLiteral, text);

        if (text[0] != '\\')
        {
            // Обычный символ – должен быть ровно один
            if (text.Length != 1)
                ThrowCompiler.Throw(ErrorCode.Parser_InvalidCharLiteral, text);
            return text[0];
        }

        // Обработка escape-последовательности
        if (text.Length < 2)
            ThrowCompiler.Throw(ErrorCode.Parser_InvalidCharLiteral, text);

        char escape = text[1];
        switch (escape)
        {
            case 'n': return '\n';
            case 't': return '\t';
            case 'r': return '\r';
            case '0': return '\0';
            case '\\': return '\\';
            case '\'': return '\'';
            case '\"': return '\"';
            case 'x':
                // \xHH (две шестнадцатеричные цифры)
                if (text.Length != 4)
                    ThrowCompiler.Throw(ErrorCode.Parser_InvalidCharLiteral, text);
                string hex = text.Substring(2, 2);
                return Convert.ToInt32(hex, 16);
            default:
                return ThrowCompiler.Throw<int>(ErrorCode.Parser_InvalidCharLiteral, text);
        }
    }

    private void Advance() => _position++;
}
