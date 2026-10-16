using Compiller.ASM;
using Compiller.ASM.Optimizators;
using Compiller.C;
using System.Data;
using System.Text.RegularExpressions;

namespace VMApplication.Project;

public sealed class VMHostProjectCompiler(
    IProjectFilesConfig paths,
    IFileService fileService)
{
    private readonly VMCompilerOptimizator _optimizator = new();
    private readonly ProjectBuilder _projectBuilder = new(fileService, paths);

    public static Regex AsmMnemonics => AsmLanguageDefinition.GetMnemonicRegex();
    public static Regex AsmRegisters => AsmLanguageDefinition.GetRegisterRegex();
    public static Regex MiniCMnemonics => MiniCLanguageDefinition.GetKeywordRegex();

    public VMCompilerOptimizator Optimizator => _optimizator;

    public CompilationToILResult CompileToIL(ulong baseAddress, bool optimize)
    {
        try
        {
            IRAssembler assembler = new(baseAddress);
            var resLog = _projectBuilder.BuildProject(optimize, assembler, _optimizator.AstOptimizer);

            OptimizationResultLog? res;
            if (optimize)
            {
                res = new();
                res.AddLog(resLog);
                
            }
            else
            {
                res = null;
            }
            return new CompilationToILResult(assembler, res);
        }
        catch (Exception ex)
        {
            return new CompilationToILResult(null, null, [ex.Message]);
        }
    }

    public CompilationResult Compile(ulong baseAddress, bool optimize)
    {
        try
        {
            AssemblerBase assembler = new Assembler(baseAddress);
            var resLog = _projectBuilder.BuildProject(optimize,assembler, _optimizator.AstOptimizer);

            OptimizationResultLog? res;
            if (optimize)
            {
                res = new();
                res.AddLog(resLog);
            }
            else
            {
                res = null;
            }

            return new CompilationResult(assembler.Build(), baseAddress, null, res);
        }
        catch (Exception ex)
        {
            return new CompilationResult(null, 0, [ex.Message], null);
        }
    }

    public CompilationResult Compile(CompilationToILResult res, int peepholeOptimizationCount)
    {
        try
        {
            var asm = res.Assembler;
            if (asm != null)
            {
                var log = new PeepholeLog();
                var peep = asm.GetItems();
                _optimizator.PeepholeOptimizer.Optimize(peep, log, peepholeOptimizationCount);
                var peepholeLog = new PeepholeOptimizationLogs(log);
                var logger = res.OptimizationResultLog;
                logger ??= new();
                logger.AddLog(peepholeLog);

                return new CompilationResult(asm.Build(), asm.BaseAddress, null, logger);
            }
            return new CompilationResult(null, 0, ["Unkown error, assembler is null"], null);

        }
        catch (Exception ex)
        {
            return new CompilationResult(null, 0, [ex.Message], null);
        }
    }

    public CompilationToILResult CompileToIL(IEnumerable<SourceFile> files, ulong baseAddress, bool optimize)
    {
        try
        {
            IRAssembler assembler = new(baseAddress);
            var resLog = _projectBuilder.Build(files, optimize, assembler, _optimizator.AstOptimizer);

            OptimizationResultLog? res = optimize ? new() : null;
            res?.AddLog(resLog);

            return new CompilationToILResult(assembler, res);
        }
        catch
        {
            return new CompilationToILResult(null, null);
        }
    }

    public CompilationResult Compile(IEnumerable<SourceFile> files, ulong baseAddress, bool optimize)
    {
        try
        {
            AssemblerBase assembler = new Assembler(baseAddress);
            var resLog = _projectBuilder.Build(files, optimize, assembler, _optimizator.AstOptimizer);

            OptimizationResultLog? res = optimize ? new() : null;
            res?.AddLog(resLog);

            return new CompilationResult(assembler.Build(), baseAddress, null, res);
        }
        catch (Exception ex)
        {
            return new CompilationResult(null, 0, [ex.Message], null);
        }
    }

    public CompilationToILResult CompileToIL(string source, ulong baseAddress, bool optimize, SourceLanguage language = SourceLanguage.C)
        => CompileToIL([new SourceFile("source", source, language)], baseAddress, optimize);

    public CompilationToILResult CompileToIL(IEnumerable<string> sources, ulong baseAddress, bool optimize, SourceLanguage language = SourceLanguage.C)
        => CompileToIL(
            sources.Select((s, i) => new SourceFile($"source_{i}", s, language)),
            baseAddress,
            optimize);


    public CompilationResult Compile(string source, ulong baseAddress, bool optimize, SourceLanguage language = SourceLanguage.C)
        => Compile([new SourceFile("source", source, language)], baseAddress, optimize);

    public CompilationResult Compile(IEnumerable<string> sources, ulong baseAddress, bool optimize, SourceLanguage language = SourceLanguage.C)
        => Compile(
            sources.Select((s, i) => new SourceFile($"source{i}", s, language)),
            baseAddress,
            optimize);
}