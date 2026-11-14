using Nantuc.Compiler.C.AST;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.Parser;

public partial class Parser
{
    // ============================================================
    // Верхний уровень: struct, #include, extern, глобальные
    // переменные и определения функций.
    // ============================================================
    private void ParseTopLevelItem(ProgramNode program)
    {
        if (IsStructDeclaration())
        {
            program.StructNodes.Add(ParseStructDeclaration());
            return;
        }

        if (Current.Type == TokenType.Operator && Current.Value == "#")
        {
            ParseIncludeDirective(program);
            return;
        }

        if (Current.Type == TokenType.Keyword && Current.Value == "extern")
        {
            ParseExternDeclaration(program);
            return;
        }

        if (IsTypeSpecifier())
        {
            ParseGlobalOrFunctionDeclaration(program);
            return;
        }

        _erRep.Throw(ErrorCode.Parser_UnexpectedToken,
            Current.Type.AsText, Current.Value, Current.Column);
    }

    private bool IsStructDeclaration()
        => Current.Type == TokenType.Keyword && Current.Value == "struct"
           && _position + 2 < _tokens.Count
           && _tokens[_position + 2].Type == TokenType.Punctuation
           && _tokens[_position + 2].Value == "{";

    private StructDeclNode ParseStructDeclaration()
    {
        Advance();                                            // "struct"
        string name = Expect(TokenType.Identifier).Value;
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

        return new StructDeclNode(name, fields);
    }

    private void ParseIncludeDirective(ProgramNode program)
    {
        Expect(TokenType.Operator, "#");
        string directive = Expect(TokenType.Identifier).Value;
        if (directive != "include")
            _erRep.Throw(ErrorCode.Parser_UnknownDirective, directive);

        string filePath = Expect(TokenType.String).Value.Trim('"');
        program.IncludesList.Add(filePath);
    }

    private void ParseExternDeclaration(ProgramNode program)
    {
        Advance();                                            // "extern"
        string type = ParseType();

        if (Current.Value == "*") Advance();

        string name = Expect(TokenType.Identifier).Value;

        if (Current.Value == "(")
        {
            program.FunctionNodes.Add(ParseExternFunction(name, type));
            return;
        }

        // extern-переменная: пропускаем до ';'
        while (Current.Value != ";") Advance();
        Advance();
    }

    private FunctionNode ParseExternFunction(string name, string returnType)
    {
        Expect(TokenType.Punctuation, "(");
        var parameters = new List<ParameterNode>();

        if (Current.Value != ")")
        {
            while (true)
            {
                string paramType = ParseType();
                bool paramIsPtr = false;
                if (Current.Value == "*") { paramIsPtr = true; Advance(); }

                string paramName = Expect(TokenType.Identifier).Value;
                parameters.Add(new ParameterNode(paramType, paramName)
                {
                    IsPointer = paramIsPtr,
                    PointedType = paramType,
                });

                if (Current.Value != ",") break;
                Expect(TokenType.Punctuation, ",");
            }
        }

        Expect(TokenType.Punctuation, ")");
        Expect(TokenType.Punctuation, ";");

        return new FunctionNode(name, returnType, null!)
        {
            IsExternal = true,
            Parameters = parameters,
        };
    }

    private void ParseGlobalOrFunctionDeclaration(ProgramNode program)
    {
        var (type, isPointer, pointedType, prefixConst, ptrConst) = ParseDeclSpec();
        string name = Expect(TokenType.Identifier).Value;

        if (Current.Type == TokenType.Punctuation && Current.Value == "(")
        {
            program.FunctionNodes.Add(ParseFunction(type, name, isPointer));
            return;
        }

        var varNodes = ParseDeclarators(type, name, isPointer, pointedType, prefixConst, ptrConst);
        program.GlobalVarNodes.AddRange(varNodes);
        Expect(TokenType.Punctuation, ";");
    }
}