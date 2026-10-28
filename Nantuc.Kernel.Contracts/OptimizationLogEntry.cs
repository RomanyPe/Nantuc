namespace Nantuc.Kernel.Contracts;

/// <summary>
/// Оптимизационный лог с защитой от перезаписи новыми логами
/// </summary>
/// <param name="Pass">проход</param>
/// <param name="Message">сообщение оптимизатора</param>
public readonly record struct OptimizationLogEntry(
    int Pass,
    string Message);