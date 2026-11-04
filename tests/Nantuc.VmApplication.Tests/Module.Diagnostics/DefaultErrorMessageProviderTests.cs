using Nantuc.Diagnostics;
using System.Collections.Frozen;

namespace Nantuc.Tests.Module.Diagnostics;

public class DefaultErrorMessageProviderTests
{
    public static TheoryData<ErrorCode> AllDefinedCodes()
    {
        var data = new TheoryData<ErrorCode>();
        foreach (var code in Enum.GetValues<ErrorCode>())
            data.Add(code);
        return data;
    }

    // ── полный обход Enum.GetValues<ErrorCode>() ─────────────────────
    [Theory]
    [MemberData(nameof(AllDefinedCodes))]
    public void GetMessage_EveryDefinedCode_DoesNotThrow(ErrorCode code)
    {
        var provider = new DefaultErrorMessageProvider();
        var message = provider.GetMessage(code);
        Assert.False(string.IsNullOrEmpty(message), $"No message for {code}");
    }

    [Fact]
    public void DefaultTemplates_HasEntryForEveryDefinedCode()
    {
        foreach (var code in Enum.GetValues<ErrorCode>())
            Assert.True(DefaultErrorMessageProvider.DefaultTemplates.ContainsKey(code),
                $"Missing default template for {code}");
    }

    // ── форматирование ───────────────────────────────────────────────
    [Fact]
    public void GetMessage_NoArgs_ReturnsTemplateVerbatim()
    {
        var provider = new DefaultErrorMessageProvider();
        Assert.Equal("Invalid assignment target",
            provider.GetMessage(ErrorCode.Parser_InvalidAssignment));
    }

    [Fact]
    public void GetMessage_OneArg_FormatsTemplate()
    {
        var provider = new DefaultErrorMessageProvider();
        Assert.Equal("Unknown type 'Foo'",
            provider.GetMessage(ErrorCode.Parser_UnknownType, (DiagnosticData)"Foo"));
    }

    [Fact]
    public void GetMessage_MultipleMixedArgs_FormatsInOrder()
    {
        var provider = new DefaultErrorMessageProvider();
        var message = provider.GetMessage(
            ErrorCode.Parser_ExpectedToken,
            (DiagnosticData)"';'",
            (DiagnosticData)"'}'",
            (DiagnosticData)10,
            (DiagnosticData)5);
        Assert.Equal("Expected ';', got '}' at 10:5", message);
    }

    // ── overrides: регресс на «замена vs мерж» ───────────────────────
    [Fact]
    public void Overrides_MergeWithDefaults_Regression()
    {
        // Раньше overrides полностью заменяли словарь, и GetMessage
        // для любого кода не из overrides падал с InvalidOperationException.
        var overrides = new Dictionary<ErrorCode, string>
        {
            [ErrorCode.Parser_UnknownType] = "custom: {0}"
        }.ToFrozenDictionary();

        var provider = new DefaultErrorMessageProvider(overrides);

        Assert.Equal("custom: Foo",
            provider.GetMessage(ErrorCode.Parser_UnknownType, (DiagnosticData)"Foo"));

        // Код, которого нет в overrides — должен по-прежнему работать.
        Assert.Equal("Invalid assignment target",
            provider.GetMessage(ErrorCode.Parser_InvalidAssignment));
    }

    [Fact]
    public void Overrides_Empty_FallsBackToDefaults()
    {
        var provider = new DefaultErrorMessageProvider(FrozenDictionary<ErrorCode, string>.Empty);
        Assert.Equal("Invalid assignment target",
            provider.GetMessage(ErrorCode.Parser_InvalidAssignment));
    }

    [Fact]
    public void Overrides_Null_FallsBackToDefaults()
    {
        var provider = new DefaultErrorMessageProvider(overrides: null);
        Assert.Equal("Invalid assignment target",
            provider.GetMessage(ErrorCode.Parser_InvalidAssignment));
    }

    [Fact]
    public void Templates_ExposesMergedView()
    {
        var overrides = new Dictionary<ErrorCode, string>
        {
            [ErrorCode.Parser_UnknownType] = "custom: {0}"
        }.ToFrozenDictionary();
        var provider = new DefaultErrorMessageProvider(overrides);

        Assert.Equal("custom: {0}", provider.Templates[ErrorCode.Parser_UnknownType]);
        Assert.Equal("Invalid assignment target", provider.Templates[ErrorCode.Parser_InvalidAssignment]);
    }

    // ── TryGetTemplate как точка расширения ──────────────────────────
    [Fact]
    public void TryGetTemplate_Override_Works()
    {
        var provider = new MarkerProvider();
        Assert.Equal("marker",
            provider.GetMessage(ErrorCode.Parser_InvalidAssignment));
        // Все прочие коды — из базового словаря.
        Assert.Equal("Not supported: x",
            provider.GetMessage(ErrorCode.NotSupported, (DiagnosticData)"x"));
    }

    private sealed class MarkerProvider : DefaultErrorMessageProvider
    {
        protected override string? TryGetTemplate(ErrorCode code)
            => code == ErrorCode.Parser_InvalidAssignment
                ? "marker"
                : base.TryGetTemplate(code);
    }
}


