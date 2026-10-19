namespace Nantuc.VmApplication.Project;

// Результат компиляции
public readonly struct CompilationResult(byte[]? program, ulong startAdress, IReadOnlyList<string>? errors, OptimizationResultLog? log)
{
    public byte[]? Program { get; } = program;
    public ulong StartAdress { get; } = startAdress;
    public OptimizationResultLog? OptimizationResultLog { get; } = log;
    public IReadOnlyList<string>? Errors { get; } = errors;
    public bool Success => Errors == null || Errors.Count == 0;
}
