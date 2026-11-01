using Nantuc.Kernel.Diagnostics;
using System.Globalization;

namespace Nantuc.Tests.Module.Diagnostics;

public class DiagnosticDataTests
{
    // ── Text и ToString ─────────────────────────────────────────────
    [Fact]
    public void Constructor_Null_BecomesEmpty()
    {
        Assert.Equal(string.Empty, new DiagnosticData(null).Text);
    }

    [Fact]
    public void ToString_ReturnsText()
    {
        Assert.Equal("abc", new DiagnosticData("abc").ToString());
    }

    [Fact]
    public void Equality_ComparesByText()
    {
        Assert.Equal(new DiagnosticData("x"), new DiagnosticData("x"));
        Assert.NotEqual(new DiagnosticData("x"), new DiagnosticData("y"));
    }

    // ── implicit conversions ───────────────────────────────────────
    [Fact]
    public void ImplicitFromString_Works()
    {
        DiagnosticData data = "hello";
        Assert.Equal("hello", data.Text);
    }

    [Fact]
    public void ImplicitFromNullString_BecomesEmpty()
    {
        string? s = null;
        DiagnosticData data = s;
        Assert.Equal(string.Empty, data.Text);
    }

    [Fact]
    public void ImplicitFromChar_Works()
    {
        DiagnosticData data = 'z';
        Assert.Equal("z", data.Text);
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(42, "42")]
    [InlineData(-1, "-1")]
    [InlineData(int.MinValue, "-2147483648")]
    [InlineData(int.MaxValue, "2147483647")]
    public void ImplicitFromInt_UsesInvariantCulture(int value, string expected)
    {
        DiagnosticData data = value;
        Assert.Equal(expected, data.Text);
    }

    [Fact]
    public void ImplicitFromUInt_UsesInvariantCulture()
    {
        DiagnosticData data = 4_000_000_000u;
        Assert.Equal("4000000000", data.Text);
    }

    [Fact]
    public void ImplicitFromLong_UsesInvariantCulture()
    {
        DiagnosticData data = long.MaxValue;
        Assert.Equal(long.MaxValue.ToString(CultureInfo.InvariantCulture), data.Text);
    }

    [Fact]
    public void ImplicitFromULong_UsesInvariantCulture()
    {
        DiagnosticData data = ulong.MaxValue;
        Assert.Equal(ulong.MaxValue.ToString(CultureInfo.InvariantCulture), data.Text);
    }

    // ── Of(bool) ───────────────────────────────────────────────────
    [Fact]
    public void OfBool_True_ReturnsLowercaseTrue()
        => Assert.Equal("true", DiagnosticData.Of(true).Text);

    [Fact]
    public void OfBool_False_ReturnsLowercaseFalse()
        => Assert.Equal("false", DiagnosticData.Of(false).Text);

    // ── Of<T> ──────────────────────────────────────────────────────
    [Fact]
    public void OfGeneric_Int_ReturnsInvariantString()
        => Assert.Equal("123", DiagnosticData.Of(123).Text);

    [Fact]
    public void OfGeneric_Reference_ReturnsToString()
        => Assert.Equal("abc", DiagnosticData.Of("abc").Text);

    [Fact]
    public void OfGeneric_NullReference_BecomesEmpty()
        => Assert.Equal(string.Empty, DiagnosticData.Of(null!).Text);

    // ── культура не влияет ─────────────────────────────────────────
    [Fact]
    public void ImplicitConversions_AreCultureIndependent()
    {
        var prev = CultureInfo.CurrentCulture;
        try
        {
            // fr-FR использует неразрывный пробел как разделитель тысяч
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            DiagnosticData data = 1234567;
            Assert.Equal("1234567", data.Text);
            Assert.DoesNotContain(" ", data.Text);
            Assert.DoesNotContain("\u00A0", data.Text);
            Assert.DoesNotContain("\u202F", data.Text);
        }
        finally
        {
            CultureInfo.CurrentCulture = prev;
        }
    }
}


