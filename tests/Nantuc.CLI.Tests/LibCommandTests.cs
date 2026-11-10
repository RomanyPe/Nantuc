namespace Nantuc.CLI.Tests;

public class LibCommandTests
{
    // --- Add: из файла через -p ---

    [Fact]
    public void Add_FromPath_AddsLibrary()
    {
        var ctx = new CliTestContext();
        ctx.WriteProjectFile("external.inc", "content");

        var r = ctx.RunLib("add", "-p", ctx.ProjectPath("external.inc"));

        Assert.Equal(0, r.ExitCode);
        Assert.Contains("external.inc", r.Stdout);
        Assert.True(ctx.IncludeFileExists("external.inc"));
        Assert.Equal(
            "content",
            ctx.FileSystem.File.ReadAllText(ctx.IncludePath("external.inc")));
    }

    [Fact]
    public void Add_FromPath_RelativePath_ResolvesAgainstCurrentDirectory()
    {
        var ctx = new CliTestContext();
        ctx.WriteProjectFile("external.inc", "content");

        // -p передаётся относительным; резолв идёт через mock CWD
        var r = ctx.RunLib("add", "-p", "external.inc");

        Assert.Equal(0, r.ExitCode);
        Assert.True(ctx.IncludeFileExists("external.inc"));
    }

    [Fact]
    public void Add_FromPath_NonexistentFile_ReturnsError()
    {
        var ctx = new CliTestContext();
        var r = ctx.RunLib("add", "-p", "no-such.inc");

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("не найден", r.Stderr);
        Assert.False(ctx.IncludeFileExists("no-such.inc"));
    }

    [Fact]
    public void Add_FromPath_InvalidPath_ReturnsError()
    {
        var ctx = new CliTestContext();
        // Символ '\0' запрещён в путях на всех ОС — GetFullPath должен бросить
        var r = ctx.RunLib("add", "-p", "bad\0name.inc");

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("некорректный путь", r.Stderr);
    }

    [Fact]
    public void Add_FromPath_ExistingLibrary_ReturnsError()
    {
        var ctx = new CliTestContext();
        ctx.WriteProjectFile("dup.inc", "new content");
        ctx.WriteIncludeFile("dup.inc", "old content");

        var r = ctx.RunLib("add", "-p", ctx.ProjectPath("dup.inc"));

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("уже существует", r.Stderr);
        Assert.Equal(
            "old content",
            ctx.FileSystem.File.ReadAllText(ctx.IncludePath("dup.inc")));
    }

    [Fact]
    public void Add_FromPath_MissingValue_ReturnsError()
    {
        var ctx = new CliTestContext();
        var r = ctx.RunLib("add", "-p");

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("требуется путь", r.Stderr);
    }

    // --- Add: из аргументов (имя + содержимое) ---

    [Fact]
    public void Add_FromArgs_AddsLibrary()
    {
        var ctx = new CliTestContext();
        var r = ctx.RunLib("add", "lib.inc", "some content");

        Assert.Equal(0, r.ExitCode);
        Assert.Contains("lib.inc", r.Stdout);
        Assert.True(ctx.IncludeFileExists("lib.inc"));
        Assert.Equal(
            "some content",
            ctx.FileSystem.File.ReadAllText(ctx.IncludePath("lib.inc")));
    }

    [Fact]
    public void Add_FromArgs_ExistingLibrary_ReturnsError()
    {
        var ctx = new CliTestContext();
        ctx.WriteIncludeFile("lib.inc", "old");

        var r = ctx.RunLib("add", "lib.inc", "new");

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("уже существует", r.Stderr);
    }

    [Fact]
    public void Add_FromArgs_InsufficientArgs_ReturnsError()
    {
        var ctx = new CliTestContext();
        var r = ctx.RunLib("add", "only-name");

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("требуется", r.Stderr);
    }

    // --- Remove ---

    [Fact]
    public void Remove_ExistingLibrary_ReturnsZero()
    {
        var ctx = new CliTestContext();
        ctx.WriteIncludeFile("lib.inc", "x");

        var r = ctx.RunLib("remove", "lib.inc");

        Assert.Equal(0, r.ExitCode);
        Assert.Contains("Удалён", r.Stdout);
        Assert.False(ctx.IncludeFileExists("lib.inc"));
    }

    [Fact]
    public void Remove_NonexistentLibrary_ReturnsError()
    {
        var ctx = new CliTestContext();
        var r = ctx.RunLib("remove", "ghost.inc");

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("не найден", r.Stderr);
    }

    [Fact]
    public void Remove_NoArgs_ReturnsError()
    {
        var ctx = new CliTestContext();
        var r = ctx.RunLib("remove");

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("требуется", r.Stderr);
    }

    // --- Exist ---

    [Fact]
    public void Exist_Existing_ReturnsZero()
    {
        var ctx = new CliTestContext();
        ctx.WriteIncludeFile("lib.inc", "x");

        var r = ctx.RunLib("exist", "lib.inc");

        Assert.Equal(0, r.ExitCode);
        Assert.Contains("Да", r.Stdout);
    }

    [Fact]
    public void Exist_Nonexistent_ReturnsOne()
    {
        var ctx = new CliTestContext();
        var r = ctx.RunLib("exist", "ghost.inc");

        Assert.Equal(1, r.ExitCode);
        Assert.Contains("Нет", r.Stdout);
    }

    [Fact]
    public void Exist_NoArgs_ReturnsError()
    {
        var ctx = new CliTestContext();
        var r = ctx.RunLib("exist");

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("требуется", r.Stderr);
    }

    // --- List ---

    [Fact]
    public void List_Empty_ReportsNoLibraries()
    {
        var ctx = new CliTestContext();
        var r = ctx.RunLib("list");

        Assert.Equal(0, r.ExitCode);
        Assert.Contains("Инклюды отсутствуют", r.Stdout);
    }

    [Fact]
    public void List_WithLibraries_PrintsAllNames()
    {
        var ctx = new CliTestContext();
        ctx.WriteIncludeFile("a.inc", "a");
        ctx.WriteIncludeFile("b.inc", "b");

        var r = ctx.RunLib("list");

        Assert.Equal(0, r.ExitCode);
        Assert.Contains("a.inc", r.Stdout);
        Assert.Contains("b.inc", r.Stdout);
        Assert.DoesNotContain("Инклюды отсутствуют", r.Stdout);
    }

    // --- Неизвестное действие ---

    [Fact]
    public void UnknownAction_ReturnsError()
    {
        var ctx = new CliTestContext();
        var r = ctx.RunLib("frobnicate");

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("неизвестное действие", r.Stderr);
        Assert.Contains("frobnicate", r.Stderr);
    }

    [Fact]
    public void NoAction_ReturnsError()
    {
        var ctx = new CliTestContext();
        var r = ctx.Run("lib");

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("не указано действие", r.Stderr);
    }
}