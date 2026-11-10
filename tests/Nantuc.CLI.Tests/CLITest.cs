using Xunit.Abstractions;

namespace Nantuc.CLI.Tests;

public class CLITest(ITestOutputHelper log)
{
    [Fact]
    public void Compile_NoSource_Succeeds()
    {
        var ctx = new CliTestContext();
        var r = ctx.Run("compile", "-s", "bin");

        DumpOnFailure(r.ExitCode == 0, log, r);
        Assert.Equal(0, r.ExitCode);
    }

    [Fact]
    public void Compile_WithOptimization_Then_Run_Returns_100()
    {
        var ctx = new CliTestContext();
        ctx.WriteProjectFile("prog.minic",
            """
            int main() {
                return 100;
            }
            """);

        var compile = ctx.Run("compile", "-s", "bin", "-o");
        DumpOnFailure(compile.ExitCode == 0, log, compile);
        Assert.Equal(0, compile.ExitCode);

        // program.bin окажется в ProjectPath/bin
        var programPath = ctx.ProjectPath(ctx.FileSystem.Path.Combine("bin", "program.bin"));
        Assert.True(ctx.FileSystem.File.Exists(programPath));

        var run = ctx.Run("run", "-p", programPath);
        DumpOnFailure(run.ExitCode == 100, log, run);
        Assert.Equal(100, run.ExitCode);
    }

    [Fact]
    public void EmptyArgs_Returns_Error_To_Stderr()
    {
        var ctx = new CliTestContext();
        var r = ctx.Run();
        Assert.NotEqual(0, r.ExitCode);
        Assert.Equal("", r.Stdout);
        Assert.Contains("No command", r.Stderr);
    }

    [Fact]
    public void UnknownCommand_Returns_Error_To_Stderr()
    {
        var ctx = new CliTestContext();
        var r = ctx.Run("bogus", "x");
        Assert.NotEqual(0, r.ExitCode);
        Assert.Equal("", r.Stdout);
        Assert.Contains("Unknown command", r.Stderr);
        Assert.Contains("bogus", r.Stderr);
    }

    [Fact]
    public void Run_With_DumbArgs()
    {
        var ctx = new CliTestContext();
        ctx.WriteProjectFile("prog.minic", "int main() { return 0; }");

        var compile = ctx.Run("compile", "-s", "bin");
        DumpOnFailure(compile.ExitCode == 0, log, compile);
        Assert.Equal(0, compile.ExitCode);

        var programPath = ctx.ProjectPath(ctx.FileSystem.Path.Combine("bin", "program.bin"));
        var pathDumb = ctx.FileSystem.Path.Combine("bin", "dumb.bin");

        var r = ctx.Run("run", "-p", programPath, "-dm", pathDumb);
        DumpOnFailure(r.ExitCode == 0, log, r);
        Assert.Equal(0, r.ExitCode);
        Assert.True(
            ctx.FileSystem.File.Exists(ctx.FileSystem.Path.Combine(ctx.Config.BaseDirectory, pathDumb)),
            "не создан дамп памяти по -dm пути");
    }
    [Fact]
    public void Run_MissingProgram_Returns_Error()
    {
        var ctx = new CliTestContext();
        var r = ctx.Run("run", "-p", "nope.bin");
        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("does not exist", r.Stderr);
    }

    [Fact]
    public void Help_Prints_Usage_To_Stdout()
    {
        var ctx = new CliTestContext();
        var r = ctx.Run("help");
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("Usage:", r.Stdout);
        Assert.Equal("", r.Stderr);
    }

    private static void DumpOnFailure(bool ok, ITestOutputHelper log, CliResult r)
    {
        if (ok) return;
        log.WriteLine($"ExitCode: {r.ExitCode}");
        log.WriteLine("--- stdout ---");
        log.WriteLine(r.Stdout);
        log.WriteLine("--- stderr ---");
        log.WriteLine(r.Stderr);
    }
}