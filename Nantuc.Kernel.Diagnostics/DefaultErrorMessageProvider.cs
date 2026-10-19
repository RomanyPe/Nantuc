using System.Collections.Frozen;

namespace Nantuc.Kernel.Diagnostics;

public class DefaultErrorMessageProvider : IErrorMessageProvider
{
    private readonly FrozenDictionary<ErrorCode, string> _templates = new Dictionary<ErrorCode, string>()
    {
        { ErrorCode.NotSupported, "Not supported: {0}" },
        { ErrorCode.NotImplemented, "Feature not implemented: {0}" },

        { ErrorCode.Lexer_UnexpectedCharacter, "Unexpected character '{0}' at line {1}, column {2}" },

        { ErrorCode.Parser_UnknownType, "Unknown type '{0}'" },
        { ErrorCode.Parser_ExpectedToken, "Expected {0}, got {1} at {2}:{3}" },
        { ErrorCode.Parser_UnexpectedToken, "Unexpected token: {0}" },
        { ErrorCode.Parser_InvalidAssignment, "Invalid assignment target" },
        { ErrorCode.Parser_ArraySizeNotConstant, "Array size must be constant" },
        { ErrorCode.Parser_UnknownDirective, "Unknown preprocessor directive: #{0}" },
        { ErrorCode.Parser_StructNotDefined, "Struct '{0}' is not defined" },
        { ErrorCode.Parser_InvalidCharLiteral, "Char literal '{0}' is invalid" },

        { ErrorCode.CodeGen_UnknownTypeSize, "Unknown type '{0}' for memory size" },
        { ErrorCode.CodeGen_UnknownOpCodeSize, "Unknown opcode size: {0}" },
        { ErrorCode.CodeGen_UnknownStructType, "Unknown struct type '{0}'" },
        { ErrorCode.CodeGen_UnknownField, "Field '{0}' not found in struct '{1}'" },
        { ErrorCode.CodeGen_NonStructAccess, "'{0}' is not a struct or pointer to struct" },
        { ErrorCode.CodeGen_InvalidDotAccess, "Dot access only for simple variables" },
        { ErrorCode.CodeGen_UndefinedVariable, "Undefined variable '{0}'" },
        { ErrorCode.CodeGen_CannotGetRegisterAddress, "Cannot take address of register variable '{0}'" },
        { ErrorCode.CodeGen_NonIndexableType, "'{0}' is not an array or pointer" },
        { ErrorCode.CodeGen_UnknownFunction, "Function '{0}' not found" },
        { ErrorCode.CodeGen_ArgumentCountMismatch, "Argument count mismatch for function '{0}'" },
        { ErrorCode.CodeGen_ParameterNotInRegister, "Parameter '{0}' not allocated to a register" },
        { ErrorCode.CodeGen_UnknownPointedType, "Cannot determine pointed type for dereference" },
        { ErrorCode.CodeGen_DirectStructLoadStore, "Direct load/store of struct variable '{0}' is not supported" },

        { ErrorCode.Asm_UndefinedLabel, "Undefined label '{0}' at position '{1}'" },
        { ErrorCode.Asm_DuplicateLabel, "Label '{0}' already defined at position {1}" },
        { ErrorCode.Asm_InvalidInstruction, "Invalid instruction syntax: {0}" },
        { ErrorCode.Asm_UnknownRegister, "Unknown register '{0}'" },
        { ErrorCode.Asm_UnknownMnemonic, "Unknown mnemonic '{0}'" },
        { ErrorCode.Lexer_InvalidNumber, "Invalid number literal '{0}' at {1}:{2}" },
        { ErrorCode.Lexer_UnterminatedString, "Unterminated string starting at {0}:{1}" }
    }.ToFrozenDictionary();

    public string GetMessage(ErrorCode code, params ReadOnlySpan<object> args)
    {
        if (!_templates.TryGetValue(code, out var template))
            throw new InvalidOperationException($"No message template for error code {code}");
        return string.Format(template, args);
    }
}
