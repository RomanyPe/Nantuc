namespace Nantuc.Kernel.Contracts;

/// <summary>
/// Запись журнала оптимизатора: номер прохода и сообщение. Иммутабельна; после создания не изменяется
/// </summary>
/// <param name="Pass">проход</param>
/// <param name="Message">сообщение оптимизатора</param>
public readonly record struct OptimizationLogEntry(
    int Pass,
    string Message);