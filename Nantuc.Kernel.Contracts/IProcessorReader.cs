using Nantuc.Kernel.Common;

namespace Nantuc.Kernel.Contracts;

/// <summary>
/// Интерфейс чтения состояния процессора
/// </summary>
public interface IProcessorReader
{
    /// <summary>
    /// Проверка находится ли процессора в режиме: работы
    /// </summary>
    bool IsRunning { get; }
    /// <summary>
    /// Проверка находится ли процессора в режиме: сна
    /// </summary>
    bool IsSleeping { get; }
    /// <summary>
    /// Получение данных регистра
    /// </summary>
    /// <param name="regType">регистр</param>
    ulong GetRegister(RegType regType);
}