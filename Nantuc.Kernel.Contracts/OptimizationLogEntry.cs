namespace Nantuc.Kernel.Contracts;

public readonly record struct OptimizationLogEntry(
    int Pass,
    string Message);