using Nantuc.Diagnostics;

namespace Nantuc.Tests.Module.Diagnostics;

public class CodeExceptionTests
{
    [Fact]
    public void ErrorCode_IsPreserved()
    {
        var ex = new CodeException(ErrorCode.CodeGen_AssignmentToConst, "msg");
        Assert.Equal(ErrorCode.CodeGen_AssignmentToConst, ex.ErrorCode);
    }

    [Fact]
    public void Message_IsPreserved()
    {
        var ex = new CodeException(ErrorCode.NotSupported, "hello");
        Assert.Equal("hello", ex.Message);
    }

    [Fact]
    public void NumericCode_MatchesEnumValue()
    {
        var ex = new CodeException(ErrorCode.Parser_UnknownType, "msg");
        Assert.Equal((int)ErrorCode.Parser_UnknownType, ex.NumericCode);
    }

    // ── стабильность числовых значений ──────────────────────────────
    // ErrorCode.cs <remarks>: «Числовые значения — стабильный контракт».
    // Хардкодим ключевые коды, чтобы случайное переупорядочивание enum
    // не проскочило в релиз.
    [Theory]
    [InlineData(ErrorCode.NotSupported, 1000)]
    [InlineData(ErrorCode.NotImplemented, 1001)]
    [InlineData(ErrorCode.Lexer_UnexpectedCharacter, 1100)]
    [InlineData(ErrorCode.Parser_UnknownType, 1200)]
    [InlineData(ErrorCode.Parser_IncludeAmbiguous, 1208)]
    [InlineData(ErrorCode.CodeGen_UnknownTypeSize, 1300)]
    [InlineData(ErrorCode.CodeGen_ContinueOutsideLoop, 1315)]
    [InlineData(ErrorCode.CodeGen_AssignmentToConst, 1316)]
    [InlineData(ErrorCode.CodeGen_IncrementOfConst, 1317)]
    [InlineData(ErrorCode.Asm_UnknownMnemonic, 1404)]
    public void NumericContract_IsStable(ErrorCode code, int expected)
    {
        var ex = new CodeException(code, "m");
        Assert.Equal(expected, ex.NumericCode);
    }
}


