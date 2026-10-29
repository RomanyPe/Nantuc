using Nantuc.Compiler.ASM;
using Nantuc.Compiler.ASM.Base;
using Nantuc.Compiler.ASM.IR;
using Nantuc.Compiler.C;
using Nantuc.Kernel.Diagnostics;
using System.Text.RegularExpressions;

namespace Nantuc.VmApplication.Project;

/// <summary>
/// Дочерний хоста - менеджер проекта
/// Содержить <see cref="IProjectFilesConfig"/>, <see cref="IFileService"/>, <see cref="VmCompilerOptimizator"/> и <see cref="ThrowCompiler"/>
/// </summary>
/// <param name="ctx"> контекст компилятора</param>
public sealed class VmHostProject(CompilerContext ctx)
{
    private readonly CompilerContext _ctx = ctx;

    /// <summary><see cref="Regex"/> мнемоник NANASM </summary>
    public static Regex AsmMnemonics => AsmLanguageDefinition.GetMnemonicRegex();
    /// <summary><see cref="Regex"/> регистров NANASM </summary
    public static Regex AsmRegisters => AsmLanguageDefinition.GetRegisterRegex();
    /// <summary><see cref="Regex"/> мнемоник MiniC </summary
    public static Regex MiniCMnemonics => MiniCLanguageDefinition.GetKeywordRegex();

    /// <summary>Оптимизаторы</summary>
    public VmCompilerOptimizator Optimizator => _ctx.Optimizator;
    /// <summary>Файловая конфигурацяи зашитая в менеджер</summary>
    public IProjectFilesConfig Paths => _ctx.Paths;
    /// <summary>Файловый сервис зашитый в менеджер</summary>
    public IFileService FileService => _ctx.FileService;
    /// <summary>
    /// Политика исключений компилятора
    /// </summary>
    public ThrowCompiler ThrowCompilerPolicy => _ctx.ThrowCompiler;
    /// <summary>
    /// Получения исходных файлов проекта
    /// </summary>
    /// <returns>
    /// <see cref="IEnumerable{SourceFile}"/>
    /// </returns>
    public IEnumerable<SourceFile> GetSourceFiles() => ProjectBuilder.GetSourceFiles(FileService, Paths);

    // Общий метод, инкапсулирующий всю логику
    internal TResult CompileInternal<TAssembler, TResult>(
        ulong baseAddress,
        bool optimize,
        IEnumerable<SourceFile> files,
        Func<ulong, TAssembler> createAssembler,
        Func<TAssembler, OptimizationResultLog?, string[]?, TResult> createResult)
        where TAssembler : AssemblerBase
    {
        OptimizationResultLog? res = null;

        try
        {
            TAssembler assembler = createAssembler(baseAddress);
            var resLog = ProjectBuilder.Build(FileService, Paths,  files, optimize, assembler, Optimizator.AstOptimizer, ThrowCompilerPolicy);

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
            return createResult(null!, res, [ex.ToString()]);
        }
    }

    /// <summary>
    /// Оптимизации для IL результатов компиляции
    /// </summary>
    /// <param name="res">Результаты компиляции в IL</param>
    /// <param name="peepholeOptimizationCount">Количество циклов Peephole оптимизаций</param>
    /// <returns>новый <see cref="CompilationToILResult"/> держащий ссылки на исходный <paramref name="res"/></returns>
    public CompilationToILResult OptimizationIL(CompilationToILResult res, int peepholeOptimizationCount)
    {
        if (res.Errors is { Count: > 0 })
            return res;

        var asm = res.Assembler;
        if (asm is null)
            return res;

        OptimizationResultLog? resOpt = null;
        try
        {
            var log = Optimizator.PeepholeOptimizer.Optimize(asm.GetItems(), peepholeOptimizationCount);
            resOpt = res.OptimizationResultLog ?? new OptimizationResultLog();
            resOpt.AddRange(log);

            return new CompilationToILResult(asm, resOpt);
        }
        catch (Exception ex)
        {
            return new CompilationToILResult(null, resOpt, [ex.Message]);
        }
    }

    /// <summary>
    /// Компиляция в IL
    /// </summary>
    /// <param name="files">Исходный код</param>
    /// <param name="baseAddress">Стартовый адрес</param>
    /// <param name="optimize">Нужны ли оптимизации AST</param>
    /// <returns> <see cref="CompilationToILResult"/> </returns>
    public CompilationToILResult CompileToIL(IEnumerable<SourceFile> files, ulong baseAddress, bool optimize)
    {
        return CompileInternal(
           baseAddress,
           optimize,
           files,
           baseAddr => new ILAssembler(baseAddr, ThrowCompilerPolicy),
           (assembler, log, errors) => new CompilationToILResult(assembler, log, errors)
       );
    }

    /// <summary>
    /// Компиляция в бинарник
    /// </summary>
    /// <param name="files">Исходный код</param>
    /// <param name="baseAddress">Стартовый адрес</param>
    /// <param name="optimize">Нужны ли оптимизации AST</param>
    /// <returns> <see cref="CompilationResult"/> </returns>
    public CompilationResult Compile(IEnumerable<SourceFile> files, ulong baseAddress, bool optimize)
    {
        return CompileInternal(
            baseAddress,
            optimize,
            files,
            baseAddr => new Assembler(baseAddr, ThrowCompilerPolicy),
            static (assembler, log, errors) => 
            errors == null 
                ? new CompilationResult(assembler, log) 
                : new CompilationResult(errors));
    }
}
