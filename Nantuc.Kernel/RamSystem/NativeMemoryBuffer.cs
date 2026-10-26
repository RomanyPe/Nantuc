using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nantuc.Kernel.RamSystem;

public unsafe sealed class NativeMemoryBuffer : IDisposable
{
    
    private readonly NativeReadOnlyView _view;

    private byte* _ptrRam;
    private readonly nuint _sizeRam;
    private bool _disposed = false;

    public bool IsDisposed => Volatile.Read(ref _disposed);
    public nuint Length => _sizeRam;
    public INativeReadOnlyBuffer NativeReadOnlyBuffer => _view;

    public NativeMemoryBuffer(RamSize sizeRam)
    {
        nuint ram = (nuint)sizeRam.Bytes;


        byte* ptr = (byte*)NativeMemory.AllocZeroed(ram);
        if (ptr == null)
            throw new OutOfMemoryException(
                $"Failed to allocate {ram} bytes for RAM.");

        
        _sizeRam = ram;
        _ptrRam = ptr;

        _view = new NativeReadOnlyView(this);
    }

    public ushort ReadUInt16(ulong address)
    {
        var value = Unsafe.ReadUnaligned<ushort>(_ptrRam + address);
        return BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
    }

    public uint ReadUInt32(ulong address)
    {
        var value = Unsafe.ReadUnaligned<uint>(_ptrRam + address);
        return BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
    }

    public ulong ReadUInt64(ulong address)
    {
        var value = Unsafe.ReadUnaligned<ulong>(_ptrRam + address);
        return BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
    }


    public void WriteUInt16(ulong address, ushort value)
    {
        if (!BitConverter.IsLittleEndian)
            value = BinaryPrimitives.ReverseEndianness(value);
        Unsafe.WriteUnaligned(_ptrRam + address, value);
    }

    public void WriteUInt32(ulong address, uint value)
    {
        if (!BitConverter.IsLittleEndian)
            value = BinaryPrimitives.ReverseEndianness(value);
        Unsafe.WriteUnaligned(_ptrRam + address, value);
    }

    public void WriteUInt64(ulong address, ulong value)
    {
        if (!BitConverter.IsLittleEndian)
            value = BinaryPrimitives.ReverseEndianness(value);
        Unsafe.WriteUnaligned(_ptrRam + address, value);
    }

    public byte this[ulong index]
    {
        get => *(_ptrRam + index);
        set => *(_ptrRam + index) = value;
    }

    public void Clear() => NativeMemory.Clear(_ptrRam, _sizeRam);

    public void Dispose()
    {
        if (!Volatile.Read(ref _disposed))
        {
            Volatile.Write(ref _disposed, true);
            NativeMemory.Free(_ptrRam);
            _ptrRam = null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LoadProgram(ReadOnlySpan<byte> prog, ulong startProg)
    {
        if (prog.Length == 0) return;

        if (startProg > _sizeRam || (ulong)prog.Length > _sizeRam - startProg)
            throw new ArgumentOutOfRangeException(nameof(startProg));

        fixed (byte* src = prog)
            NativeMemory.Copy(src, _ptrRam + startProg, (nuint)prog.Length);
    }
}


public class NativeReadOnlyView(NativeMemoryBuffer buffer) : INativeReadOnlyBuffer
{
    public nuint Length => buffer.Length;

    public byte this[nuint index]
    {
        get
        {
            ObjectDisposedException.ThrowIf(buffer.IsDisposed, buffer);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, buffer.Length);
            return buffer[index];
        }
    }
}