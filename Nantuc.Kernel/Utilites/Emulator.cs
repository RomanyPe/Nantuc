using Nantuc.Kernel.BiosSystem;
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;
using Nantuc.Kernel.ControllersData;
using System.Diagnostics.CodeAnalysis;

namespace Nantuc.Kernel.Utilites;

/// <summary>
/// Эмулятор устройства: управляет загрузкой программ, запуском, пошаговым выполнением.
/// Теперь поддерживает несколько устройств через ManagerDevices.
/// </summary>
/// <remarks>
/// <para>
/// Владеет <see cref="PortBus"/> и <see cref="ManagerDevices"/>. Создаёт экземпляры
/// <see cref="Device"/> через <see cref="CreateDevice"/>, регистрирует внешние
/// <see cref="IPortController"/> через <see cref="RegisterDevice"/>.
/// </para>
/// <para>
/// Потокобезопасность не гарантируется: экземпляр предназначен для использования
/// из одного потока-координатора. Регистрация/снятие устройств во время работы
/// программы не синхронизированы с исполнением на процессоре.
/// </para>
/// </remarks>
public class Emulator : IDisposable
{
    private readonly PortBus _portBus;
    private readonly ManagerDevices _manager;


    /// <summary>
    /// Создаёт эмулятор с заданными размерами адресного пространства портов.
    /// </summary>
    /// <param name="ports">Общее количество портов ввода-вывода (степень двойки).</param>
    /// <param name="portsOnDev">Количество портов на одно устройство (степень двойки, не больше <paramref name="ports"/>).</param>
    /// <exception cref="ArgumentException">
    /// Если <paramref name="ports"/> или <paramref name="portsOnDev"/> не инициализированы,
    /// либо <paramref name="portsOnDev"/> превышает <paramref name="ports"/>.
    /// </exception>
    public Emulator(PortSize ports, DevicePortSize portsOnDev)
    {
        _portBus = new PortBus(ports, portsOnDev);
        _manager = new ManagerDevices(_portBus);
    }

    /// <summary>
    /// Снимок информации обо всех зарегистрированных (живых) устройствах.
    /// </summary>
    public IReadOnlyList<DeviceInfo> AllDeviceInfo => _manager.GetAllDevices();

    /// <summary>
    /// Шина портов ввода-вывода, используемая всеми устройствами эмулятора.
    /// </summary>
    public PortBus PortBus => _portBus;

    /// <summary>
    /// Регистрирует внешний контроллер портов как устройство эмулятора.
    /// </summary>
    /// <param name="device">Контроллер портов устройства.</param>
    /// <param name="name">Необязательное имя устройства для диагностики; может быть <see langword="null"/>.</param>
    /// <param name="sector">Номер сектора портов, закрепляемого за устройством.</param>
    /// <param name="id">
    /// Идентификатор, присвоенный устройству при успехе; <c>-1</c> при ошибке.
    /// </param>
    /// <returns>
    /// <see cref="IdState.Valid"/> при успехе; иначе — причина отказа
    /// (<see cref="IdState.NotAssigned"/>, <see cref="IdState.OutOfRange"/>,
    /// <see cref="IdState.NoFreeSlot"/>, <see cref="IdState.Rejected"/>).
    /// </returns>
    public IdState RegisterDevice(
        IPortController device,
        string? name,
        uint sector, out int id)
    {
        return _manager.TryAddDevice(device, name, sector, out id);
    }

    /// <summary>
    /// Создаёт новое вычислительное устройство с собственной ОЗУ и процессором.
    /// </summary>
    /// <param name="size">Размер ОЗУ устройства.</param>
    /// <param name="processorFaultPolicy">Политика обработки сбоев процессора.</param>
    /// <returns>Новый экземпляр <see cref="Device"/>.</returns>
    public Device CreateDevice(
        RamSize size,
        IProcessorFaultPolicy processorFaultPolicy)
    {
        return new Device(_portBus, size, processorFaultPolicy);
    }


    /// <summary>
    /// Возвращает контроллер портов зарегистрированного устройства по идентификатору.
    /// </summary>
    /// <param name="id">Идентификатор устройства.</param>
    /// <returns>Контроллер или <see langword="null"/>, если устройство не найдено/освобождено.</returns>
    public IPortController? GetDevice(int id) => _manager.GetDevice(id);

    /// <summary>
    /// Пытается получить контроллер устройства, приведённый к типу <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Ожидаемый тип контроллера.</typeparam>
    /// <param name="id">Идентификатор устройства.</param>
    /// <param name="device">Контроллер при успехе; иначе <see langword="null"/>.</param>
    /// <returns><see langword="true"/>, если устройство найдено и имеет тип <typeparamref name="T"/>.</returns>
    public bool TryGetDevice<T>(int id, [NotNullWhen(true)] out T? device)
        where T : class, IPortController => _manager.TryGetDevice(id, out device);


    /// <summary>
    /// Снимает устройство с регистрации и освобождает занятый им сектор портов.
    /// </summary>
    /// <param name="id">Идентификатор устройства.</param>
    /// <returns><see langword="true"/>, если устройство было снято; иначе <see langword="false"/>.</returns>
    public bool UnregisterDevice(int id) => _manager.RemoveDevice(id);

    /// <summary>
    /// Переводит устройство на другой сектор портов.
    /// </summary>
    /// <param name="id">Идентификатор устройства.</param>
    /// <param name="newSector">Новый сектор портов.</param>
    /// <returns><see langword="true"/> при успехе; иначе <see langword="false"/>.</returns>
    public bool ChangeDeviceSector(int id, uint newSector) => _manager.ChangeSector(id, newSector);


    /// <summary>
    /// Освобождает ресурсы эмулятора и всех зарегистрированных устройств.
    /// </summary>
    /// <remarks>Повторный вызов безопасен.</remarks>
    public void Dispose()
    {
        _manager.Dispose();
        GC.SuppressFinalize(this);
    }
}