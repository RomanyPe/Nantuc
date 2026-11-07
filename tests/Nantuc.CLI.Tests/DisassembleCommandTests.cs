using Nantuc.CLI.Commands;
using System.Text;
using Xunit.Abstractions;

namespace TestsCLI;

public class DisassembleCommandTests(ITestOutputHelper log)
{
    private static string WriteProgram(CliTestContext ctx, params byte[] bytes)
    {
        ctx.WriteProjectFile("prog.bin", Encoding.Latin1.GetString(bytes));
        return ctx.ProjectPath("prog.bin");
    }

    // --- TryCreate: валидация ---

    [Fact]
    public void TryCreate_NoFlags_ReturnsFalse_AndWritesToError()
    {
        var ctx = new CliTestContext();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        bool ok = DisassembleCommand.TryCreate([], ctx.Service, stdout, stderr, out var cmd);

        Assert.False(ok);
        Assert.Null(cmd);
        Assert.Equal("", stdout.ToString());
        Assert.Contains("missing required flag", stderr.ToString());
    }

    [Fact]
    public void TryCreate_WithoutCodeSize_UsesDefault()
    {
        var ctx = new CliTestContext();
        var path = WriteProgram(ctx, 0x01, 0x02, 0x03, 0x04);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        bool ok = DisassembleCommand.TryCreate(["-f", path], ctx.Service, stdout, stderr, out var cmd);

        Assert.True(ok);
        Assert.NotNull(cmd);
        Assert.Equal("", stderr.ToString());
        Assert.Equal(0, cmd.Execute());
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    public void TryCreate_InvalidCodeSize_ReturnsFalse(string bad)
    {
        var ctx = new CliTestContext();
        var path = WriteProgram(ctx, 0x01, 0x02, 0x03, 0x04);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        bool ok = DisassembleCommand.TryCreate(
            ["-cs", bad, "-f", path], ctx.Service, stdout, stderr, out var cmd);

        Assert.False(ok);
        Assert.Null(cmd);
        Assert.Contains("invalid code size", stderr.ToString());
    }

    [Fact]
    public void TryCreate_MissingFileFlag_ReturnsFalse()
    {
        var ctx = new CliTestContext();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        bool ok = DisassembleCommand.TryCreate(
            ["-cs", "4"], ctx.Service, stdout, stderr, out var cmd);

        Assert.False(ok);
        Assert.Null(cmd);
        Assert.Contains("missing required flag", stderr.ToString());
    }

    [Fact]
    public void TryCreate_NonexistentFile_ReturnsFalse()
    {
        var ctx = new CliTestContext();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        bool ok = DisassembleCommand.TryCreate(
            ["-cs", "4", "-f", "no-such-file.bin"], ctx.Service, stdout, stderr, out var cmd);

        Assert.False(ok);
        Assert.Contains("does not exist", stderr.ToString());
    }

    [Fact]
    public void TryCreate_Valid_ReturnsTrue_WithoutSideEffects()
    {
        var ctx = new CliTestContext();
        var path = WriteProgram(ctx, 0x01, 0x02, 0x03, 0x04);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        bool ok = DisassembleCommand.TryCreate(
            ["-cs", "4", "-f", path], ctx.Service, stdout, stderr, out var cmd);

        Assert.True(ok);
        Assert.NotNull(cmd);
        Assert.Equal("", stdout.ToString());
        Assert.Equal("", stderr.ToString());
    }

    // --- Execute ---

    [Fact]
    public void Execute_NoSave_WritesDisassemblyToOutput()
    {
        var ctx = new CliTestContext();
        var path = WriteProgram(ctx, 0x01, 0x02, 0x03, 0x04);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var cmd = CreateRequired(["-cs", "4", "-f", path], ctx, stdout, stderr);
        int exit = cmd.Execute();

        DumpOnFailure(exit == 0 && stdout.ToString().Length > 0, log, stdout, stderr);
        Assert.Equal(0, exit);
        Assert.False(string.IsNullOrWhiteSpace(stdout.ToString()));
    }

    [Fact]
    public void Execute_WithSave_WritesFileIntoGivenDirectory()
    {
        var ctx = new CliTestContext();
        var path = WriteProgram(ctx, 0x01, 0x02, 0x03, 0x04);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var cmd = CreateRequired(["-cs", "4", "-f", path, "-s", "out"], ctx, stdout, stderr);
        int exit = cmd.Execute();

        Assert.Equal(0, exit);
        Assert.Contains("result saved", stdout.ToString());
        Assert.True(
            ctx.FileSystem.File.Exists(ctx.ProjectPath(ctx.FileSystem.Path.Combine("out", "result-decompile.txt"))),
            "ожидалось out/result-decompile.txt — если false, -s проигнорирован");
        Assert.False(
            ctx.FileSystem.File.Exists(ctx.FileSystem.Path.Combine(path, "result-decompile.txt")),
            "не должно создавать result-decompile.txt внутри пути к файлу");
    }

    // --- интеграция с compile ---

    [Fact]
    public void Compile_Then_Disassemble_ProducesOutput()
    {
        var ctx = new CliTestContext();
        ctx.WriteProjectFile("prog.minic", "int main() { return 100; }");

        var compile = ctx.Run("compile", "-s", "bin");
        DumpOnFailure(compile.ExitCode == 0, log, compile);
        Assert.Equal(0, compile.ExitCode);

        var programPath = ctx.ProjectPath(ctx.FileSystem.Path.Combine("bin", "program.bin"));
        Assert.True(ctx.FileSystem.File.Exists(programPath));

        var disasm = ctx.Run("disasm", "-cs", "32", "-f", programPath);
        DumpOnFailure(disasm.ExitCode == 0, log, disasm);
        Assert.Equal(0, disasm.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(disasm.Stdout));
    }

    // --- helpers ---

    private static DisassembleCommand CreateRequired(
        string[] args, CliTestContext ctx, TextWriter stdout, TextWriter stderr)
    {
        bool ok = DisassembleCommand.TryCreate(args, ctx.Service, stdout, stderr, out var cmd);
        Assert.True(ok, stderr is StringWriter sw ? sw.ToString() : "");
        return cmd!;
    }

    private static void DumpOnFailure(
        bool ok, ITestOutputHelper log, StringWriter stdout, StringWriter stderr)
    {
        if (ok) return;
        log.WriteLine("--- stdout ---");
        log.WriteLine(stdout.ToString());
        log.WriteLine("--- stderr ---");
        log.WriteLine(stderr.ToString());
    }
    private static void DumpOnFailure(
        bool ok, ITestOutputHelper log, CliResult res)
    {
        if (ok) return;
        log.WriteLine("--- stdout ---");
        log.WriteLine(res.Stdout.ToString());
        log.WriteLine("--- stderr ---");
        log.WriteLine(res.Stderr.ToString());
    }
}