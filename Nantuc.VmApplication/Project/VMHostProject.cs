using Nantuc.Compiler.ASM;
using Nantuc.Compiler.ASM.Base;
using Nantuc.Compiler.ASM.IR;
using Nantuc.Compiler.C;
using System.Text.RegularExpressions;

namespace Nantuc.VmApplication.Project;

/// <summary>
/// Дочерний хоста - менеджер проекта
/// Содержить <see cref="IProjectFilesConfig"/> и <see cref="IFileService"/>
/// </summary>
/// <param name="paths">Файловая конфигурация</param>
/// <param name="fileService">Файловый сервис</param>
public sealed class VmHostProject(IProjectFilesConfig paths, IFileService fileService, VmCompilerOptimizator optimizator)
{
    private readonly IProjectFilesConfig _projectPaths = paths;
    private readonly IFileService _fileService = fileService;
    private readonly VmCompilerOptimizator _optimizator = optimizator;

    /// <summary><see cref="Regex"/> мнемоник NANASM </summary>
    public static Regex AsmMnemonics => AsmLanguageDefinition.GetMnemonicRegex();
    /// <summary><see cref="Regex"/> регистров NANASM </summary
    public static Regex AsmRegisters => AsmLanguageDefinition.GetRegisterRegex();
    /// <summary><see cref="Regex"/> мнемоник MiniC </summary
    public static Regex MiniCMnemonics => MiniCLanguageDefinition.GetKeywordRegex();

    /// <summary>Оптимизаторы</summary>
    public VmCompilerOptimizator Optimizator => _optimizator;
    /// <summary>Файловая конфигурацяи зашитая в менеджер</summary>
    public IProjectFilesConfig Paths => _projectPaths;
    /// <summary>Файловый сервис зашитый в менеджер</summary>
    public IFileService FileService => _fileService;
    /// <summary>
    /// Получения исходных файлов проекта
    /// </summary>
    /// <returns>
    /// <see cref="IEnumerable{SourceFile}"/>
    /// </returns>
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
        OptimizationResultLog? res = null;

        try
        {
            TAssembler assembler = createAssembler(baseAddress);
            var resLog = ProjectBuilder.Build(_fileService, _projectPaths,  files, optimize, assembler, _optimizator.AstOptimizer);

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
            var log = _optimizator.PeepholeOptimizer.Optimize(asm.GetItems(), peepholeOptimizationCount);
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
           baseAddr => new ILAssembler(baseAddr),
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
            baseAddr => new Assembler(baseAddr),
            static (assembler, log, errors) => 
            errors == null 
                ? new CompilationResult(assembler, log) 
                : new CompilationResult(errors));
    }
}
