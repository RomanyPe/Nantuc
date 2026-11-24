using Nantuc.Diagnostics;
using System.Collections.Frozen;

namespace Nantuc.VmApplication.Extensions.Diagnostics;

public static class DiagnosticsCompilerExtension
{
    public static FrozenSet<ErrorCode> ErrorCodesValidateTable { get; } = 
        [   ErrorCode.NotSupported,
            ErrorCode.NotImplemented,

            ErrorCode.Lexer_UnexpectedCharacter,
            ErrorCode.Lexer_InvalidNumber,
            ErrorCode.Lexer_UnterminatedString,

            ErrorCode.Parser_UnknownType,
            ErrorCode.Parser_ExpectedToken,
            ErrorCode.Parser_UnexpectedToken,
            ErrorCode.Parser_InvalidAssignment,
            ErrorCode.Parser_ArraySizeNotConstant,
            ErrorCode.Parser_UnknownDirective,
            ErrorCode.Parser_StructNotDefined,
            ErrorCode.Parser_InvalidCharLiteral,
            ErrorCode.Parser_IncludeAmbiguous,
            ErrorCode.Parser_IncludeNotFound,

            ErrorCode.CodeGen_UnknownTypeSize,
            ErrorCode.CodeGen_UnknownOpCodeSize,
            ErrorCode.CodeGen_UnknownStructType,
            ErrorCode.CodeGen_UnknownField,
            ErrorCode.CodeGen_NonStructAccess,
            ErrorCode.CodeGen_InvalidDotAccess,
            ErrorCode.CodeGen_UndefinedVariable,
            ErrorCode.CodeGen_CannotGetRegisterAddress,
            ErrorCode.CodeGen_NonIndexableType,
            ErrorCode.CodeGen_UnknownFunction,
            ErrorCode.CodeGen_ArgumentCountMismatch,
            ErrorCode.CodeGen_ParameterNotInRegister,
            ErrorCode.CodeGen_UnknownPointedType,
            ErrorCode.CodeGen_DirectStructLoadStore,
            ErrorCode.CodeGen_BreakOutsideLoop,
            ErrorCode.CodeGen_ContinueOutsideLoop,
            ErrorCode.CodeGen_AssignmentToConst,
            ErrorCode.CodeGen_IncrementOfConst,

            ErrorCode.Asm_UndefinedLabel,
            ErrorCode.Asm_DuplicateLabel,
            ErrorCode.Asm_InvalidInstruction,
            ErrorCode.Asm_UnknownRegister,
            ErrorCode.Asm_UnknownMnemonic];

    extension(IReadOnlyDictionary<ErrorCode, string> templates)
    {
        public void ValidateAllCodesCovered()
        {
            foreach (var c in ErrorCodesValidateTable)
                if (!templates.ContainsKey(c))
                    throw new InvalidOperationException($"Missing message template for error code {c}");
        }

        public bool TryValidateAllCodesCovered(out ErrorCode code)
        {
            foreach (var c in ErrorCodesValidateTable)
                if (!templates.ContainsKey(c))
                {
                    code = c;
                    return false;
                }
            code = default;
            return true;
        }
    }
}
