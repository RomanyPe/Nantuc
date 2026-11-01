namespace Nantuc.Kernel.Contracts;

/// <summary>
/// Интерфейс реализующий <see cref="IPortUse"/> и <see cref="IDisposable"/>
/// </summary>
/// <remarks>
/// dispose is called by the bus owner; the device must not be used after dispose
/// </remarks>
public interface IPortController : IPortUse, IDisposable;