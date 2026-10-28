using Nantuc.Kernel.Common;

namespace Nantuc.Kernel.Contracts;

/// <summary>
/// Результат чтения порта из <see cref="IPortBus"/>
/// </summary>
/// <param name="Data">данные</param>
/// <param name="Status">статус ошибки</param>
public readonly record struct ReadPortResult(byte Data, BiosStatus Status = BiosStatus.Success);
