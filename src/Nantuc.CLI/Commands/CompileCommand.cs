using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Extensions.Optimizators;
using Nantuc.VmApplication.Project;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Nantuc.CLI.Commands;

public class CompileCommand(bool needOpt, string pathToSave, IFileService fileService, TextWriter wr, IProjectFilesConfig config)
{
    public static bool HaveOptimizationArg(ReadOnlySpan<string> args) => CLIHelper.HaveFlag(["-opt", "-o"], args);

    public static bool TryGetSavePath(ReadOnlySpan<string> args, [NotNullWhen(true)] out string? path)
    {
        return CLIHelper.TryGetArgByFlag(["-sv", "-save", "-s"], args, out path);
    }
    public static bool TryGetIncludePath(ReadOnlySpan<string> args, [NotNullWhen(true)] out string? path)
    {
        return CLIHelper.TryGetArgByFlag(["-inc", "-include", "-i"], args, out path);
    }

    public static bool TryCreate(
        ReadOnlySpan<string> args,
        IFileService fileService,
        TextWriter wr,
        IProjectFilesConfig config,
        [NotNullWhen(true)] out CompileCommand? command)
    {
        if (!TryGetSavePath(args, out var savePath))
        {
            command = null;
            return false;
        }
        command = new CompileCommand(HaveOptimizationArg(args), savePath, fileService, wr, config);
        return true;
    }

    public int Execute()
    {
        VmCompilerOptimizator? opt = null;
        if (needOpt)
        {
            opt = new();
            opt.VmAstOptimizer.AddStdRules();
            opt.VmPeepholeOptimizer.AddStdRules();
        }

        var proj = new VmHostProjectBuilder()
            .WithFileService(fileService)
            .WithPaths(config)
            .WithOptimizator(opt)
            .Build();

        CompilationResult code;
        if (needOpt)
        {
            var res = proj.CompileToIL(ProjectBuilder.BaseAddressProgram, needOpt);
            res = proj.OptimizationIL(res, 5);
            code = res.Build();
        }
        else
        {
            code = proj.Compile(ProjectBuilder.BaseAddressProgram, needOpt);
        }

        if (!code.Success)
        {
            foreach (var er in code.Errors ?? [])
                wr.WriteLine(er);
            return -1;
        }
        string s = Encoding.Latin1.GetString(code.Program!);
        proj.FileService.SaveFile(proj.FileService.Combine(pathToSave, "program.bin"), s);
        return 0;
    }
}
