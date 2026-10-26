namespace Nantuc.Kernel.Diagnostics;

public enum ErrorCode
{
    // Общие
    NotSupported = 1000,
    NotImplemented = 1001,

    // Лексер
    Lexer_UnexpectedCharacter = 1100,
    Lexer_InvalidNumber = 1101,
    Lexer_UnterminatedString = 1102,

    // Парсер
    Parser_UnknownType = 1200,
    Parser_ExpectedToken = 1201,
    Parser_UnexpectedToken = 1202,
    Parser_InvalidAssignment = 1203,
    Parser_ArraySizeNotConstant = 1204,
    Parser_UnknownDirective = 1205,
    Parser_StructNotDefined = 1206,
    Parser_InvalidCharLiteral = 1207,

    // Генератор кода (CodeGen)
    CodeGen_UnknownTypeSize = 1300,
    CodeGen_UnknownOpCodeSize = 1301,
    CodeGen_UnknownStructType = 1302,
    CodeGen_UnknownField = 1303,
    CodeGen_NonStructAccess = 1304,
    CodeGen_InvalidDotAccess = 1305,
    CodeGen_UndefinedVariable = 1306,
    CodeGen_CannotGetRegisterAddress = 1307,
    CodeGen_NonIndexableType = 1308,
    CodeGen_UnknownFunction = 1309,
    CodeGen_ArgumentCountMismatch = 1310,
    CodeGen_ParameterNotInRegister = 1311,
    CodeGen_UnknownPointedType = 1312,
    CodeGen_DirectStructLoadStore = 1313,
    CodeGen_BreakOutsideLoop = 1314,
    CodeGen_ContinueOutsideLoop = 1315,

    // Ассемблер
    Asm_UndefinedLabel = 1400,
    Asm_DuplicateLabel = 1401,
    Asm_InvalidInstruction = 1402,
    Asm_UnknownRegister = 1403,
    Asm_UnknownMnemonic = 1404,
}
