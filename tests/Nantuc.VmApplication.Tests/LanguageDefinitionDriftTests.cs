using Nantuc.Compiler.ASM;
using Nantuc.Compiler.C;

namespace Nantuc.Tests;

// ============================================================
// Гарантирует, что Regex и FrozenSet не разъезжаются.
// Именно отсутствие такого теста позволило drift'у с "new"
// прожить незамеченным.
// ============================================================
public class LanguageDefinitionDriftTests
{
    [Fact]
    public void MiniC_KeywordRegex_matches_every_keyword()
    {
        foreach (var kw in MiniCLanguageDefinition.Keywords)
            Assert.Matches(MiniCLanguageDefinition.KeywordRegex, kw);
    }

    [Fact]
    public void MiniC_KeywordRegex_does_not_match_non_keywords()
    {
        // Исторический drift-кейс: "new" был в паттерне, но не в Keywords
        Assert.DoesNotMatch(MiniCLanguageDefinition.KeywordRegex, "new");
        Assert.DoesNotMatch(MiniCLanguageDefinition.KeywordRegex, "foo");
        Assert.DoesNotMatch(MiniCLanguageDefinition.KeywordRegex, "int32");
    }

    [Fact]
    public void Asm_MnemonicRegex_matches_every_mnemonic()
    {
        foreach (var m in AsmLanguageDefinition.Mnemonics)
            Assert.Matches(AsmLanguageDefinition.MnemonicRegex, m);
    }

    [Fact]
    public void Asm_RegisterRegex_matches_every_register()
    {
        foreach (var r in AsmLanguageDefinition.Registers)
            Assert.Matches(AsmLanguageDefinition.RegisterRegex, r);
    }

    [Fact]
    public void Asm_RegisterRegex_does_not_match_prefix()
    {
        // \b(r1|r10)\b не должен матчить "r10" как "r1"
        var match = AsmLanguageDefinition.RegisterRegex.Match("r10");
        Assert.Equal("r10", match.Value);
    }
}
