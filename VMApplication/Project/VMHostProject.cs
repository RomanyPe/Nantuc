using Compiller.ASM;
using Compiller.ASM.Optimizators;
using Compiller.C;
using System.Text.RegularExpressions;

namespace VMApplication.Project;

public readonly struct CompilationToILResult
{
    internal readonly IRAssembler? Assembler;
    public IReadOnlyList<string>? Errors { get; }
    internal OptimizationResultLog? OptimizationResultLog { get; }
    internal CompilationToILResult(IRAssembler? assembler, OptimizationResultLog? optimizationResultLog, IReadOnlyList<string>? errors = null)
    {
        Assembler = assembler;
        OptimizationResultLog = optimizationResultLog;
        Errors = errors;
    }

    public CompilationResult Build()
    {
        try
        {
            var asm = Assembler;
            return asm != null
                ? new CompilationResult(asm.Build(), asm.BaseAddress, null, OptimizationResultLog)
                : new CompilationResult(null, 0, Errors, null);
        }
        catch (Exception ex)
        {
            return new CompilationResult(null, 0, [ex.Message], null);
        }
    }
}

public sealed class VMHostProject(IProjectFilesConfig paths, IFileService fileService)
{
    private readonly IProjectFilesConfig _projectPaths = paths;
    private readonly IFileService _fileService = fileService;
    private readonly VMCompilerOptimizator _optimizator = new();
    private readonly ProjectBuilder _projectBuilder = new(fileService, paths);

    public static Regex AsmMnemonics => AsmLanguageDefinition.GetMnemonicRegex();
    public static Regex AsmRegisters => AsmLanguageDefinition.GetRegisterRegex();
    public static Regex MiniCMnemonics => MiniCLanguageDefinition.GetKeywordRegex();

    public VMCompilerOptimizator Optimizator => _optimizator;
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
            var resLog = _projectBuilder.Build(files, optimize, assembler, _optimizator.AstOptimizer);

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
            (assembler, log, errors) => new CompilationResult(errors == null ? assembler.Build() : null, baseAddress, errors, log)
        );
    }
}
