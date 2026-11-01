using Nantuc.Kernel.Diagnostics;
using System.Collections.Frozen;

namespace Nantuc.Tests.Module.Diagnostics;

public class ThrowCompilerTests
{
    // ── Throw ────────────────────────────────────────────────────────
    [Fact]
    public void Throw_RaisesCodeExceptionWithCodeAndMessage()
    {
        var thrower = new ThrowCompiler(null);
        var ex = Assert.Throws<CodeException>(
            () => thrower.Throw(ErrorCode.Parser_UnknownType, (DiagnosticData)"Foo"));

        Assert.Equal(ErrorCode.Parser_UnknownType, ex.ErrorCode);
        Assert.Equal("Unknown type 'Foo'", ex.Message);
    }

    [Fact]
    public void Throw_NoArgs_UsesTemplateVerbatim()
    {
        var thrower = new ThrowCompiler(null);
        var ex = Assert.Throws<CodeException>(
            () => thrower.Throw(ErrorCode.Parser_InvalidAssignment));
        Assert.Equal("Invalid assignment target", ex.Message);
    }

    // ── Throw<T> ─────────────────────────────────────────────────────
    [Fact]
    public void ThrowGeneric_UsableInExpression()
    {
        var thrower = new ThrowCompiler(null);
        string? value = null;

        var ex = Assert.Throws<CodeException>(() => PassThrough(thrower, value));

        Assert.Equal(ErrorCode.NotSupported, ex.ErrorCode);
        Assert.StartsWith("Not supported:", ex.Message);
    }

    private static string PassThrough(ThrowCompiler thrower, string? value)
        => value ?? thrower.Throw<string>(ErrorCode.NotSupported, (DiagnosticData)"missing");

    [Fact]
    public void ThrowGeneric_NonNull_ReturnsValue()
    {
        var thrower = new ThrowCompiler(null);
        Assert.Equal("ok", PassThrough(thrower, "ok"));
    }

    // ── конструктор ──────────────────────────────────────────────────
    [Fact]
    public void Constructor_Null_UsesDefaultProvider()
    {
        var thrower = new ThrowCompiler(provider: null);
        Assert.Same(IErrorMessageProvider.Default, thrower.ErrorMessageProvider);
    }

    [Fact]
    public void Constructor_Custom_StoresProvider()
    {
        var custom = new DefaultErrorMessageProvider();
        var thrower = new ThrowCompiler(custom);
        Assert.Same(custom, thrower.ErrorMessageProvider);
    }

    // ── изоляция провайдеров ────────────────────────────────────────
    [Fact]
    public void TwoThrowCompilers_DifferentProviders_DoNotInterfere()
    {
        var custom = new DefaultErrorMessageProvider(new Dictionary<ErrorCode, string>
        {
            [ErrorCode.NotSupported] = "custom: {0}"
        }.ToFrozenDictionary());

        var a = new ThrowCompiler(provider: null);
        var b = new ThrowCompiler(custom);

        var exA = Assert.Throws<CodeException>(
            () => a.Throw(ErrorCode.NotSupported, (DiagnosticData)"X"));
        var exB = Assert.Throws<CodeException>(
            () => b.Throw(ErrorCode.NotSupported, (DiagnosticData)"X"));

        Assert.StartsWith("Not supported", exA.Message);
        Assert.StartsWith("custom:", exB.Message);
    }
}


