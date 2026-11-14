using Nantuc.Compiler.C.AST;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.Parser;

public partial class Parser
{
    // ============================================================
    // Типы, деклараторы, объявления переменных.
    // ============================================================
    private static readonly HashSet<string> BuiltinTypes =
    [
        "int", "char", "void", "byte", "ushort", "ulong", "short", "long", "uint"
    ];

    private bool IsTypeSpecifier()
    {
        if (Current.Type == TokenType.Keyword && Current.Value == "const") return true;
        if (Current.Type == TokenType.Keyword && BuiltinTypes.Contains(Current.Value)) return true;
        if (Current.Type == TokenType.Identifier && _structNames.Contains(Current.Value)) return true;
        if (Current.Type == TokenType.Keyword && Current.Value == "struct") return true;
        return false;
    }

    /// <summary>
    /// Полная спецификация: [const] type [const] [* [const]]
    /// </summary>
    private (string type, bool isPointer, string? pointedType,
             bool prefixConst, bool ptrConst) ParseDeclSpec()
    {
        bool prefixConst = false;
        if (Current.Type == TokenType.Keyword && Current.Value == "const")
        {
            prefixConst = true;
            Advance();
        }

        string type = ParseType();

        // int const x  ≡  const int x
        if (Current.Type == TokenType.Keyword && Current.Value == "const")
        {
            prefixConst = true;
            Advance();
        }

        bool isPointer = false;
        bool ptrConst = false;
        string? pointedType = null;

        if (Current.Value == "*")
        {
            isPointer = true;
            pointedType = type;
            Advance();
            if (Current.Type == TokenType.Keyword && Current.Value == "const")
            {
                ptrConst = true;
                Advance();
            }
        }

        return (type, isPointer, pointedType, prefixConst, ptrConst);
    }

    private string ParseType()
    {
        if (Current.Type == TokenType.Keyword && Current.Value == "struct")
        {
            Advance();
            string name = Expect(TokenType.Identifier).Value;
            if (!_structNames.Contains(name))
                _erRep.Throw(ErrorCode.CodeGen_UnknownStructType, name);
            return name;
        }
        if (Current.Type == TokenType.Identifier && _structNames.Contains(Current.Value))
            return Expect(TokenType.Identifier).Value;
        return Expect(TokenType.Keyword).Value;
    }

    private FunctionNode ParseFunction(string returnType, string name, bool isReturnPointer)
    {
        Expect(TokenType.Punctuation, "(");
        var parameters = new List<ParameterNode>();

        if (Current.Value != ")")
        {
            while (true)
            {
                var (paramType, paramIsPtr, paramPointed, paramPrefixConst, paramPtrConst) = ParseDeclSpec();
                string paramName = Expect(TokenType.Identifier).Value;

                parameters.Add(new ParameterNode(paramType, paramName)
                {
                    IsPointer = paramIsPtr,
                    PointedType = paramPointed,
                    IsConst = paramIsPtr ? paramPtrConst : paramPrefixConst,
                    IsPointedConst = paramIsPtr && paramPrefixConst,
                });

                if (Current.Value != ",") break;
                Expect(TokenType.Punctuation, ",");
            }
        }

        Expect(TokenType.Punctuation, ")");
        Expect(TokenType.Punctuation, "{");
        var body = ParseBlock();

        return new FunctionNode(name, returnType, body)
        {
            Parameters = parameters,
            ReturnIsPointer = isReturnPointer,
        };
    }

    private List<VariableNode> ParseDeclarators(
        string type, string firstName,
        bool firstIsPointer, string? firstPointedType,
        bool firstPrefixConst, bool firstPtrConst)
    {
        var result = new List<VariableNode>
        {
            ParseVariableTail(type, firstName, firstIsPointer, firstPointedType,
                              firstPrefixConst, firstPtrConst),
        };

        while (Current.Type == TokenType.Punctuation && Current.Value == ",")
        {
            Advance();

            bool isPtr = false;
            bool ptrConst = false;
            string? pointed = null;

            if (Current.Type == TokenType.Operator && Current.Value == "*")
            {
                isPtr = true;
                pointed = type;
                Advance();
                if (Current.Type == TokenType.Keyword && Current.Value == "const")
                {
                    ptrConst = true;
                    Advance();
                }
            }

            string name = Expect(TokenType.Identifier).Value;
            // prefixConst применяется к каждому декларатору: const int *a, b; -> b тоже const
            result.Add(ParseVariableTail(type, name, isPtr, pointed, firstPrefixConst, ptrConst));
        }
        return result;
    }

    private VariableNode ParseVariableTail(
        string type, string name, bool isPointer, string? pointedType,
        bool prefixConst, bool ptrConst)
    {
        if (Current.Type == TokenType.Punctuation && Current.Value == "[")
            return ParseArrayVariableTail(type, name, isPointer, pointedType, prefixConst, ptrConst);
        return ParseScalarVariableTail(type, name, isPointer, pointedType, prefixConst, ptrConst);
    }

    private VariableNode ParseArrayVariableTail(
        string type, string name, bool isPointer, string? pointedType,
        bool prefixConst, bool ptrConst)
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
            PointedType = pointedType,
            IsConst = ptrConst,             // int * const arr[] — экзотика
            IsPointedConst = prefixConst,   // const int arr[] -> элементы неизменяемы
        };

        if (Current.Type == TokenType.Operator && Current.Value == "=")
        {
            Expect(TokenType.Operator, "=");
            ParseArrayInitializer(varNode, type, name, size);
        }
        else if (size < 0)
        {
            // int arr[]; без инициализатора — размер неизвестен
            _erRep.Throw(ErrorCode.Parser_ArraySizeNotConstant, name);
        }

        return varNode;
    }

    private void ParseArrayInitializer(VariableNode varNode, string type, string name, int size)
    {
        // char s[] = "..." — строковый литерал
        if (type == "char" && Current.Type == TokenType.String)
        {
            var initList = ParseStringToNodes(Current.Value);
            Advance();
            varNode.ArrayInitializer = initList;

            if (size < 0) varNode.ArraySize = initList.Count;
            else if (initList.Count > size)
                _erRep.Throw(ErrorCode.Parser_InvalidAssignment, name);
            return;
        }

        Expect(TokenType.Punctuation, "{");
        List<ASTNode> list = [];
        if (Current.Value != "}")
        {
            while (true)
            {
                list.Add(ParseAssignment());
                if (Current.Value != ",") break;
                Expect(TokenType.Punctuation, ",");
            }
        }
        Expect(TokenType.Punctuation, "}");

        varNode.ArrayInitializer = list;
        if (size < 0) varNode.ArraySize = list.Count;               // int arr[] = {...}
        else if (list.Count > size)
            _erRep.Throw(ErrorCode.Parser_InvalidAssignment, name); // слишком много инициализаторов
    }

    private VariableNode ParseScalarVariableTail(
        string type, string name, bool isPointer, string? pointedType,
        bool prefixConst, bool ptrConst)
    {
        ASTNode? initializer = null;
        if (Current.Type == TokenType.Operator && Current.Value == "=")
        {
            Expect(TokenType.Operator, "=");
            initializer = ParseAssignment();
        }

        return new VariableNode(type, name, initializer)
        {
            IsPointer = isPointer,
            PointedType = pointedType,
            IsConst = isPointer ? ptrConst : prefixConst,
            IsPointedConst = isPointer && prefixConst,
        };
    }
}