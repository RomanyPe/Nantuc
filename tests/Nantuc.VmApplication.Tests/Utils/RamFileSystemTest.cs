using Nantuc.VmApplication.Default;
using Nantuc.VmApplication.Project;

namespace Nantuc.Tests.Utils;

public class RamFileServiceTests
{
    private sealed class TestConfig : IProjectFilesConfig
    {
        public string BaseDirectory { get; init; } = "/base";
        public string ProjectPath { get; init; } = "/base/project";
        public string IncludePath { get; init; } = "/base/include";
        public IReadOnlyList<string> ExtensionsAsm { get; init; } = [".asm", ".nanasm"];
        public IReadOnlyList<string> ExtensionsMiniC { get; init; } = [".c", ".minic"];
    }

    // ================================================================
    // CRUD
    // ================================================================

    [Fact]
    public void SaveFile_ThenExist_ReturnsTrue()
    {
        var fs = new RamFileService();
        fs.SaveFile("a.minic", "x");
        Assert.True(fs.Exist("a.minic"));
    }

    [Fact]
    public void SaveFile_ThenRead_ReturnsSameContent()
    {
        var fs = new RamFileService();
        fs.SaveFile("a.minic", "content");
        Assert.Equal("content", fs.ReadFile("a.minic"));
    }

    [Fact]
    public void Exist_ForUnknownFile_ReturnsFalse()
    {
        var fs = new RamFileService();
        Assert.False(fs.Exist("nope.minic"));
    }

    [Fact]
    public void ReadFile_ForUnknownFile_ThrowsKeyNotFound()
    {
        var fs = new RamFileService();
        Assert.Throws<KeyNotFoundException>(() => fs.ReadFile("nope.minic"));
    }

    [Fact]
    public void SaveFile_OverwritesContent()
    {
        var fs = new RamFileService();
        fs.SaveFile("a.minic", "first");
        fs.SaveFile("a.minic", "second");
        Assert.Equal("second", fs.ReadFile("a.minic"));
    }

    // ================================================================
    // Нормализация путей
    // ================================================================

    [Theory]
    [InlineData("src/main.minic")]
    [InlineData(@"src\main.minic")]
    [InlineData("./src/main.minic")]
    [InlineData("src//main.minic")]
    [InlineData("src/./main.minic")]
    [InlineData("src/sub/../main.minic")]
    public void Exist_ResolvesEquivalentPathForms(string lookupPath)
    {
        var fs = new RamFileService();
        fs.SaveFile("src/main.minic", "x");
        Assert.True(fs.Exist(lookupPath));
    }

    [Fact]
    public void Save_WithBackslashes_ReadWithSlashes()
    {
        var fs = new RamFileService();
        fs.SaveFile(@"src\main.minic", "content");
        Assert.Equal("content", fs.ReadFile("src/main.minic"));
    }

    [Fact]
    public void AbsoluteAndRelativePaths_AreDistinctFiles()
    {
        var fs = new RamFileService();
        fs.SaveFile("/file.minic", "x");
        Assert.True(fs.Exist("/file.minic"));
        Assert.False(fs.Exist("file.minic"));
    }

    [Fact]
    public void ParentRef_DoesNotEscapeAbsoluteRoot()
    {
        var fs = new RamFileService();
        fs.SaveFile("/file.minic", "x");
        Assert.True(fs.Exist("/../file.minic"));
    }

    // ================================================================
    // Регистрозависимость
    // ================================================================

    [Fact]
    public void Exist_IsCaseInsensitiveByDefault()
    {
        var fs = new RamFileService();
        fs.SaveFile("src/Main.minic", "x");
        Assert.True(fs.Exist("src/main.MINIC"));
    }

    [Fact]
    public void Exist_IsCaseSensitive_WhenConfigured()
    {
        var fs = new RamFileService(caseSensitive: true);
        fs.SaveFile("src/Main.minic", "x");
        Assert.False(fs.Exist("src/main.minic"));
        Assert.True(fs.Exist("src/Main.minic"));
    }

    // ================================================================
    // Combine
    // ================================================================

    [Theory]
    [InlineData("src", "main.minic", "src/main.minic")]
    [InlineData("src/", "main.minic", "src/main.minic")]
    [InlineData("src", "./main.minic", "src/main.minic")]
    [InlineData("src", "sub/main.minic", "src/sub/main.minic")]
    [InlineData("src/sub", "../main.minic", "src/main.minic")]
    [InlineData(@"src\sub", "main.minic", "src/sub/main.minic")]
    public void Combine_ProducesNormalizedPath(string p1, string p2, string expected)
    {
        var fs = new RamFileService();
        Assert.Equal(expected, fs.Combine(p1, p2));
    }

    [Fact]
    public void Combine_ThenSave_ThenRead_RoundTrips()
    {
        var fs = new RamFileService();
        var path = fs.Combine("src", "main.minic");
        fs.SaveFile(path, "hello");
        Assert.Equal("hello", fs.ReadFile("src/main.minic"));
    }

    // ================================================================
    // GetNameFiles — реальные расширения (.asm/.nanasm/.c/.minic)
    // ================================================================

    [Theory]
    [InlineData("a.c", true)]
    [InlineData("a.minic", true)]
    [InlineData("a.asm", true)]
    [InlineData("a.nanasm", true)]
    [InlineData("a.cpp", false)]
    [InlineData("a.txt", false)]
    [InlineData("a", false)]
    public void GetNameFiles_FiltersByRealExtensions(string fileName, bool expected)
    {
        var fs = new RamFileService();
        fs.SaveFile(fileName, "x");

        var result = fs.GetNameFiles(new TestConfig()).ToList();

        Assert.Equal(expected, result.Contains(fileName));
    }

    [Fact]
    public void GetNameFiles_IsCaseSensitiveForExtension_AsInOriginal()
    {
        // Оригинал сравнивает расширения StringComparison.Ordinal.
        // ".MINIC" не считается расширением ".minic".
        var fs = new RamFileService();
        fs.SaveFile("a.MINIC", "1");
        fs.SaveFile("b.minic", "2");

        var result = fs.GetNameFiles(new TestConfig()).ToList();

        Assert.Contains("b.minic", result);
        Assert.DoesNotContain("a.MINIC", result);
    }

    [Fact]
    public void GetNameFiles_ExcludesFilesUnderIncludePath()
    {
        var fs = new RamFileService();
        var config = new TestConfig { IncludePath = "/base/include" };
        fs.SaveFile("/base/include/header.minic", "h");
        fs.SaveFile("/base/project/main.minic", "m");

        var result = fs.GetNameFiles(config).ToList();

        Assert.DoesNotContain("/base/include/header.minic", result);
        Assert.Contains("/base/project/main.minic", result);
    }

    [Theory]
    [InlineData("/base/include")]
    [InlineData("/base/./include")]
    [InlineData("/base//include/")]
    public void GetNameFiles_IncludePath_IsNormalizedBeforeComparison(string includePath)
    {
        var fs = new RamFileService();
        fs.SaveFile("/base/include/header.minic", "h");
        fs.SaveFile("/base/project/main.minic", "m");

        var config = new TestConfig { IncludePath = includePath };
        var result = fs.GetNameFiles(config).ToList();

        Assert.DoesNotContain("/base/include/header.minic", result);
        Assert.Contains("/base/project/main.minic", result);
    }

    [Fact]
    public void GetNameFiles_IncludePath_IsCaseInsensitiveByDefault()
    {
        var fs = new RamFileService();
        var config = new TestConfig { IncludePath = "/base/include" };
        fs.SaveFile("/BASE/INCLUDE/header.minic", "h");
        fs.SaveFile("/base/project/main.minic", "m");

        var result = fs.GetNameFiles(config).ToList();

        Assert.DoesNotContain("/BASE/INCLUDE/header.minic", result);
        Assert.Contains("/base/project/main.minic", result);
    }

    [Fact]
    public void GetNameFiles_WithDefaultConfig_WorksOnEmptyService()
    {
        var fs = new RamFileService();
        Assert.Empty(fs.GetNameFiles(IProjectFilesConfig.Default));
    }

    [Fact]
    public void GetNameFiles_WithEmptyExtensions_ReturnsNothing()
    {
        var fs = new RamFileService();
        fs.SaveFile("a.minic", "1");

        var config = new TestConfig
        {
            ExtensionsMiniC = [],
            ExtensionsAsm = [],
        };

        Assert.Empty(fs.GetNameFiles(config));
    }

    [Fact]
    public void GetNameFiles_WithNullExtensions_DoesNotThrow()
    {
        var fs = new RamFileService();
        fs.SaveFile("a.minic", "1");

        var config = new TestConfig
        {
            ExtensionsMiniC = null!,
            ExtensionsAsm = null!,
        };

        Assert.Empty(fs.GetNameFiles(config));
    }

    [Fact]
    public void GetNameFiles_IncludePath_DoesNotMatchSiblingDirectoryWithPrefix()
    {
        var fs = new RamFileService();
        var config = new TestConfig { IncludePath = "/base/include" };
        fs.SaveFile("/base/include2/main.minic", "x"); // это НЕ в include/

        var result = fs.GetNameFiles(config).ToList();

        // Ожидается: файл должен остаться в выборке.
        // Текущая реализация исключает его — это баг.
        Assert.Contains("/base/include2/main.minic", result);
    }
}