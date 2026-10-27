using Nantuc.Compiler.ASM.Base;

namespace Nantuc.VmApplication.Project;

/// <summary>
/// Результат компиляции
/// </summary>
public readonly struct CompilationResult
{
    /// <summary>
    /// Конструктор успешной компиляции
    /// </summary>
    /// <param name="asm"> Экзмепляр Ассемблера </param>
    /// <param name="log"> Экземпляр логов оптимизации </param>
    public CompilationResult(AssemblerBase asm, OptimizationResultLog? log)
    {
        Program = asm.Build();
        StartAdress = asm.BaseAddress;
        CodeSize = asm.CodeSectionSize;
        DataStart = asm.DataSectionStart;
        DataSize = asm.DataSectionSize;
        OptimizationResultLog = log;
        Errors = null;
    }

    /// <summary>
    /// Конструктор ошибок
    /// </summary>
    /// <param name="errors">Список ошибок</param>
    public CompilationResult(IReadOnlyList<string>? errors)
    {
        Program = null;
        StartAdress = 0;
        CodeSize = -1;
        DataStart = -1;
        DataSize = -1;
        OptimizationResultLog = null;
        Errors = errors;
    }
    /// <summary>Готовый байт код программы</summary>
    public byte[]? Program { get; }
    /// <summary>Стартовый адрес программы</summary>
    public ulong StartAdress { get; }
    /// <summary>Размер .code секции</summary>
    public int CodeSize { get; }
    /// <summary>Начала .data секции</summary>
    public int DataStart { get; }
    /// <summary>Размер .data секции</summary>
    public int DataSize { get; }

    /// <summary> Логи оптимизации </summary>
    public OptimizationResultLog? OptimizationResultLog { get; }
    /// <summary> Список ошибок </summary>
    public IReadOnlyList<string>? Errors { get; }
    /// <summary> Успешность выполненной компиляции </summary>
    public bool Success => Errors == null || Errors.Count == 0;
}
