namespace Kernel.Contracts;

public interface IPortUse
{
    public byte ReadPort(ulong offset);
    public void WritePort(ulong offset, byte value);
    public void WakeProcessor();
}
