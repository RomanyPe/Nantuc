using Nantuc.Kernel.BiosSystem;
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;
using Nantuc.Kernel.ControllersData;
using System.Diagnostics.CodeAnalysis;

namespace Nantuc.VmApplication.Emulator;

/// <summary>
/// API эмулятора, СОЗДАВАТЬ ТОЛЬКО С ПОМОЩЬЮ <see cref="VmEmulatorBuilder"/>, только при владении <see cref="Kernel.Utilites.Emulator"/> создавать вручную.
/// </summary>
/// <param name="emulator">Класс низкоуровнего эмулятора из слоя <![CDATA[Kernel]]></param>
public sealed class VmEmulator(Kernel.Utilites.Emulator emulator) : IDisposable
{
    private readonly Kernel.Utilites.Emulator _emulator = emulator;
    internal PortBus AsPortBus => _emulator.PortBus;

    /// <summary>
    /// Интерфейс шины портов
    /// </summary>
    public IPortBus GetPortBus() => _emulator.PortBus;
   
    /// <summary>
    /// Смена сектора устройства с одного на другой
    /// </summary>
    /// <param name="id">Id устройство в системе</param>
    /// <param name="newSector">Его новый сектор</param>
    /// <returns>Успех смены сектора</returns>
    public bool ChangeDeviceSector(int id, uint newSector) => _emulator.ChangeDeviceSector(id, newSector);

    /// <summary>
    /// Снапшот всех устройств в системе, содержит <see cref="DeviceInfo"/>
    /// </summary>
    public IReadOnlyCollection<DeviceInfo> GetAllDevices()
        => [.. _emulator.AllDeviceInfo];

    /// <summary>
    /// Метод для регистрации устройства реализующего <see cref="IPortController"/>, не рекомендуется если регистрация происходит динамически, тоесть параметры не константны
    /// </summary>
    /// <param name="ioDevice">устройство контракта <strong>IO</strong></param>
    /// <param name="name">имя устройства</param>
    /// <param name="sector">сектор в шине портов где будет зарегестрирован</param>
    /// <returns>возвращает либо код ошибки (все отрицательные числа), либо Id в системе</returns>
    public int RegisterDevice(IPortController ioDevice,
        string? name,
        uint sector)
    {
        _emulator.RegisterDevice(ioDevice, name, sector, out int id);
        return id;
    }

    /// <summary>
    /// Метод для создания и регистрации Nantuc устройства, не рекомендуется если регистрация происходит динамически, тоесть параметры не константны
    /// </summary>
    /// <param name="spec">паспорт устройства, то с какими параметрами должен создастся</param>
    /// <param name="sector">сектор в шине портов где будет зарегестрирован</param>
    /// <returns>возвращает либо код ошибки (все отрицательные числа), либо Id в системе</returns>
    public int RegisterDevice(DeviceSpec spec, uint sector)
    {
        var device = _emulator.CreateDevice(spec.RamSize, spec.ProcessorFaultPolicy);
        return RegisterDevice(device, spec.Name, sector);
    }

    /// <summary>
    /// Try- версия <see cref="RegisterDevice(IPortController, string, uint)"/>, желателен для динамически регистрируемых устройств
    /// </summary>
    /// <param name="ioDevice">устройство контракта <strong>IO</strong></param>
    /// <param name="name">имя устройства</param>
    /// <param name="sector">сектор в шине портов где будет зарегестрирован</param>
    /// <param name="id">код ошибки (все отрицательные числа), либо Id в системе</param>
    /// <returns>enum версия кода ошибки, либо <see cref="IdState.Valid"/> если все хорошо</returns>
    public IdState TryRegisterDevice(IPortController ioDevice,
        string? name,
        uint sector, out int id)
    {
        return _emulator.RegisterDevice(ioDevice, name, sector, out id);
    }

    /// <summary>
    /// Try- версия <see cref="RegisterDevice(DeviceSpec, uint)"/>, желателен для динамически создаваемых устройств
    /// </summary>
    /// <param name="spec">паспорт устройства, то с какими параметрами должен создастся</param>
    /// <param name="sector">сектор в шине портов где будет зарегестрирован</param>
    /// <param name="id">код ошибки (все отрицательные числа), либо Id в системе</param>
    /// <returns>enum версия кода ошибки, либо <see cref="IdState.Valid"/> если все хорошо</returns>
    public IdState TryRegisterDevice(DeviceSpec spec,
        uint sector, out int id)
    {
        var device = _emulator.CreateDevice(spec.RamSize, spec.ProcessorFaultPolicy);
        return _emulator.RegisterDevice(device, spec.Name, sector, out id);
    }
    /// <summary>
    /// Метод для получения контекста Nantuc устройства, не рекомендуется если регистрация происходит динамически, тоесть параметры не константны
    /// </summary>
    /// <param name="id">Id где предположительно, находится Nantuc устройство</param>
    /// <returns>контекс Nantuc устройства, если это не Nantuc устройство вернут null</returns>
    public DeviceContext? GetDeviceContext(int id)
    {
        return _emulator.TryGetDevice<Device>(id, out var device)
            ? new DeviceContext(device)
            : null;
    }

    /// <summary>
    /// Try- версия <see cref="GetDeviceContext(int)"/>, желателен для динамического получения устройства
    /// </summary>
    /// <param name="id">Id где предположительно, находится Nantuc устройство</param>
    /// <param name="ctx">контекс Nantuc устройства</param>
    /// <returns>успех получения Nantuc устройства</returns>
    public bool TryGetDeviceContext(int id, [NotNullWhen(true)] out DeviceContext? ctx)
    {
        ctx = _emulator.TryGetDevice<Device>(id, out var device) ? new DeviceContext(device) : null;
        return ctx != null;
    }

    
    /// <summary>
    /// Удаляет устройства из системы и вызывает у него <see cref="IDisposable.Dispose"/> 
    /// </summary>
    /// <param name="id">Id устройства</param>
    /// <returns>успех удаления</returns>
    public bool UnregisterDevice(int id) => _emulator.UnregisterDevice(id);

    public void Dispose() => _emulator.Dispose();
}

public readonly record struct DeviceSpec(RamSize RamSize, string? Name, IProcessorFaultPolicy ProcessorFaultPolicy)
{
    /// <summary>
    /// Метод для создания паспорта Nantuc устройства, желательно использовать вместо конструктора, так как имеет default значения
    /// </summary>
    /// <param name="ramSize">размер озу Nantuc устройства</param>
    /// <param name="name">Имя в системе</param>
    /// <param name="processorFaultPolicy">политика остановки</param>
    public static DeviceSpec Create(RamSize? ramSize = null,
                                      string? name = null,
                                      IProcessorFaultPolicy? processorFaultPolicy = null)
    {
        ramSize ??= RamSize.MB1;
        processorFaultPolicy ??= IProcessorFaultPolicy.Default;
        return new(ramSize.Value, name, processorFaultPolicy);

    }
}