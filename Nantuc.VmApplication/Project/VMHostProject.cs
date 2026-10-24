using Nantuc.Compiler.ASM;
using Nantuc.Compiler.ASM.Base;
using Nantuc.Compiler.ASM.IR;
using Nantuc.Compiler.C;
using System.Text.RegularExpressions;

namespace Nantuc.VmApplication.Project;

public sealed class VmHostProject(IProjectFilesConfig paths, IFileService fileService)
{
    private readonly IProjectFilesConfig _projectPaths = paths;
    private readonly IFileService _fileService = fileService;
    private readonly VmCompilerOptimizator _optimizator = new();

    public static Regex AsmMnemonics => AsmLanguageDefinition.GetMnemonicRegex();
    public static Regex AsmRegisters => AsmLanguageDefinition.GetRegisterRegex();
    public static Regex MiniCMnemonics => MiniCLanguageDefinition.GetKeywordRegex();

    public VmCompilerOptimizator Optimizator => _optimizator;
    public IProjectFilesConfig Paths => _projectPaths;
    public IFileService FileService => _fileService;

    public IEnumerable<SourceFile> GetSourceFiles() => ProjectBuilder.GetSourceFiles(_fileService, Paths);
    // Общий метод, инкапсулирующий всю логику
    private TResult CompileInternal<TAssembler, TResult>(
        ulong baseAddress,
        bool optimize,
        IEnumerable<SourceFile> files,
        Func<ulong, TAssembler> createAssembler,
        Func<TAssembler, OptimizationResultLog?, string[]?, TResult> createResult)
        where TAssembler : AssemblerBase
    {
        try
        {
            TAssembler assembler = createAssembler(baseAddress);
            var resLog = ProjectBuilder.Build(_fileService, _projectPaths,  files, optimize, assembler, _optimizator.AstOptimizer);

            OptimizationResultLog? res = null;
            if (optimize)
            {
                res = new OptimizationResultLog();
                res.AddRange(resLog);
            }

            return createResult(assembler, res, null);
        }
        catch (Exception ex)
        {
            // Передаем null вместо ассемблера и лога в случае ошибки
            return createResult(null!, null, [ex.Message]);
        }
    }

    public CompilationToILResult OptimizationIL(CompilationToILResult res, int peepholeOptimizationCount)
    {
        if (res.Errors is { Count: > 0 })
            return res;

        var asm = res.Assembler;
        if (asm is null)
            return res;

        try
        {
            var log = _optimizator.PeepholeOptimizer.Optimize(asm.GetItems(), peepholeOptimizationCount);
            var logger = res.OptimizationResultLog ?? new OptimizationResultLog();
            logger.AddRange(log);

            return new CompilationToILResult(asm, logger);
        }
        catch (Exception ex)
        {
            return new CompilationToILResult(null, res.OptimizationResultLog, [ex.Message]);
        }
    }


    public CompilationToILResult CompileToIL(IEnumerable<SourceFile> files, ulong baseAddress, bool optimize)
    {
        return CompileInternal(
           baseAddress,
           optimize,
           files,
           baseAddr => new IRAssembler(baseAddr),
           (assembler, log, errors) => new CompilationToILResult(assembler, log, errors)
       );
    }

    public CompilationResult Compile(IEnumerable<SourceFile> files, ulong baseAddress, bool optimize)
    {
        return CompileInternal(
            baseAddress,
            optimize,
            files,
            baseAddr => new Assembler(baseAddr),
            static (assembler, log, errors) => 
            errors == null 
                ? new CompilationResult(assembler, log) 
                : new CompilationResult(errors));
    }
}
