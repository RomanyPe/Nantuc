using Nantuc.Compiler.ASM.Base;

namespace Nantuc.VmApplication.Project;

// Результат компиляции
public readonly struct CompilationResult
{
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

    // Конструктор ошибки
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

    public byte[]? Program { get; }
    public ulong StartAdress { get; }
    public int CodeSize { get; }
    public int DataStart { get; }
    public int DataSize { get; }
    public OptimizationResultLog? OptimizationResultLog { get; }
    public IReadOnlyList<string>? Errors { get; }
    public bool Success => Errors == null || Errors.Count == 0;
}
