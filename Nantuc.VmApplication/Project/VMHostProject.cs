using Nantuc.Compiler.ASM;
using Nantuc.Compiler.ASM.Base;
using Nantuc.Compiler.ASM.IR;
using Nantuc.Compiler.C;
using Nantuc.Kernel.Diagnostics;
using System.Text.RegularExpressions;

namespace Nantuc.VmApplication.Project;

/// <summary>
/// Дочерний хост — менеджер проекта компилятора.
/// </summary>
/// <remarks>
/// Инкапсулирует доступ к <see cref="IProjectFilesConfig"/>,
/// <see cref="IFileService"/>, <see cref="VmCompilerOptimizator"/>
/// и <see cref="ThrowCompiler"/> из переданного <see cref="CompilerContext"/>,
/// а также предоставляет высокоуровневые операции компиляции.
/// </remarks>
/// <param name="ctx">Контекст компилятора.</param>
public sealed class VmHostProject(CompilerContext ctx)
{
    private readonly CompilerContext _ctx = ctx;

    /// <summary>
    /// Регулярное выражение мнемоник NANASM.
    /// </summary>
    public static Regex AsmMnemonics => AsmLanguageDefinition.GetMnemonicRegex();

    /// <summary>
    /// Регулярное выражение регистров NANASM.
    /// </summary>
    public static Regex AsmRegisters => AsmLanguageDefinition.GetRegisterRegex();

    /// <summary>
    /// Регулярное выражение ключевых слов (не мнемоник) MiniC.
    /// </summary>
    /// <remarks>
    /// Метод <see cref="MiniCLanguageDefinition.GetKeywordRegex"/>
    /// возвращает регулярное выражение ключевых слов языка, а не мнемоник.
    /// </remarks>
    public static Regex MiniCMnemonics => MiniCLanguageDefinition.GetKeywordRegex();

    /// <summary>
    /// Оптимизатор, связанный с текущим контекстом компилятора.
    /// </summary>
    public VmCompilerOptimizator Optimizator => _ctx.Optimizator;

    /// <summary>
    /// Файловая конфигурация проекта, связанная с менеджером.
    /// </summary>
    public IProjectFilesConfig Paths => _ctx.Paths;

    /// <summary>
    /// Файловый сервис проекта, связанный с менеджером.
    /// </summary>
    public IFileService FileService => _ctx.FileService;

    /// <summary>
    /// Политика исключений компилятора.
    /// </summary>
    public ThrowCompiler ThrowCompilerPolicy => _ctx.ThrowCompiler;

    /// <summary>
    /// Перечисляет исходные файлы проекта.
    /// </summary>
    /// <returns>Последовательность <see cref="SourceFile"/> проекта.</returns>
    /// <remarks>
    /// Файлы определяются на основе <see cref="Paths"/> и читаются через
    /// <see cref="FileService"/>.
    /// </remarks>
    public IEnumerable<SourceFile> GetSourceFiles() => ProjectBuilder.GetSourceFiles(FileService, Paths);

    /// <summary>
    /// Общая реализация компиляции: создаёт ассемблер, строит проект
    /// и упаковывает результат через <paramref name="createResult"/>.
    /// </summary>
    /// <typeparam name="TAssembler">Тип ассемблера.</typeparam>
    /// <typeparam name="TResult">Тип результата компиляции.</typeparam>
    /// <param name="baseAddress">Стартовый адрес.</param>
    /// <param name="optimize">Нужны ли оптимизации AST.</param>
    /// <param name="files">Исходные файлы проекта.</param>
    /// <param name="createAssembler">Фабрика ассемблера по стартовому адресу.</param>
    /// <param name="createResult">
    /// Фабрика результата. Первый аргумент — ассемблер
    /// (<see langword="null"/> при ошибке), второй — лог оптимизаций,
    /// третий — массив текстов ошибок либо <see langword="null"/> при успехе.
    /// </param>
    /// <returns>Результат, созданный <paramref name="createResult"/>.</returns>
    /// <remarks>
    /// <para>
    /// Исключения на этапе компиляции перехватываются и передаются
    /// в <paramref name="createResult"/> в виде массива строк; ассемблер
    /// в этом случае равен <see langword="null"/>.
    /// </para>
    /// <para>
    /// Лог оптимизаций (<paramref name="optimize"/> = <see langword="true"/>)
    /// создаётся заново на каждый вызов и не переиспользуется.
    /// </para>
    /// </remarks>
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
            var resLog = ProjectBuilder.Build(FileService, Paths, files, optimize, assembler, Optimizator.AstOptimizer, ThrowCompilerPolicy);

            if (optimize)
            {
                res = new OptimizationResultLog();
                res.AddRange(resLog);
            }

            return createResult(assembler, res, null);
        }
        catch (Exception ex)
        {
            // Передаём null вместо ассемблера и лога в случае ошибки.
            return createResult(null!, res, [ex.ToString()]);
        }
    }

    /// <summary>
    /// Применяет Peephole-оптимизации к результату компиляции в IL.
    /// </summary>
    /// <param name="res">Результат компиляции в IL.</param>
    /// <param name="peepholeOptimizationCount">Количество циклов Peephole-оптимизаций.</param>
    /// <returns>
    /// Новый <see cref="CompilationToILResult"/>, ссылающийся на исходный
    /// <paramref name="res"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Если в <paramref name="res"/> уже есть ошибки
    /// (<see cref="CompilationToILResult.Errors"/> непуст) или ассемблер
    /// отсутствует, метод возвращает <paramref name="res"/> без изменений.
    /// </para>
    /// <para>
    /// Если <see cref="CompilationToILResult.OptimizationResultLog"/> у
    /// <paramref name="res"/> уже задан, он <b>мутируется</b> (дополняется
    /// результатом Peephole-оптимизации), а не копируется. При необходимости
    /// сохранить исходный лог передавайте копию.
    /// </para>
    /// <para>
    /// Исключения при оптимизации не пробрасываются: возвращается новый
    /// <see cref="CompilationToILResult"/> с <see langword="null"/> вместо
    /// ассемблера и текстом исключения в ошибках.
    /// </para>
    /// </remarks>
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
    /// Компилирует исходные файлы в IL-ассемблер.
    /// </summary>
    /// <param name="files">Исходные файлы проекта.</param>
    /// <param name="baseAddress">Стартовый адрес.</param>
    /// <param name="optimize">Нужны ли оптимизации AST.</param>
    /// <returns>
    /// <see cref="CompilationToILResult"/>. При ошибке компиляции —
    /// с <see langword="null"/> вместо ассемблера и текстом исключения
    /// в <see cref="CompilationToILResult.Errors"/>.
    /// </returns>
    /// <remarks>
    /// Исключения компиляции перехватываются внутри
    /// <see cref="CompileInternal{TAssembler,TResult}"/> и возвращаются
    /// в результате, а не пробрасываются наружу. Проверяйте
    /// <see cref="CompilationToILResult.Errors"/>.
    /// </remarks>
    public CompilationToILResult CompileToIL(IEnumerable<SourceFile> files, ulong baseAddress, bool optimize)
    {
        return CompileInternal(
            baseAddress,
            optimize,
            files,
            baseAddr => new ILAssembler(baseAddr, ThrowCompilerPolicy),
            (assembler, log, errors) => new CompilationToILResult(assembler, log, errors));
    }

    /// <summary>
    /// Компилирует исходные файлы в бинарный ассемблер.
    /// </summary>
    /// <param name="files">Исходные файлы проекта.</param>
    /// <param name="baseAddress">Стартовый адрес.</param>
    /// <param name="optimize">Нужны ли оптимизации AST.</param>
    /// <returns>
    /// <see cref="CompilationResult"/>. При ошибке — содержит только ошибки
    /// (лог оптимизаций не передаётся).
    /// </returns>
    /// <remarks>
    /// <para>
    /// Исключения компиляции перехватываются внутри
    /// <see cref="CompileInternal{TAssembler,TResult}"/> и возвращаются
    /// в результате, а не пробрасываются наружу.
    /// </para>
    /// <para>
    /// В отличие от <see cref="CompileToIL"/>, при ошибке
    /// лог оптимизаций в результат не попадает: создаётся
    /// <see cref="CompilationResult"/> только с ошибками.
    /// </para>
    /// </remarks>
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