using Nantuc.Common;
using Nantuc.Contracts;
using Nantuc.Kernel.Utilities;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Unsafe;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Nantuc.VmApplication.Extensions.AsyncDeviceExtensions;

public static class AsyncDeviceExtension
{
    extension(VmEmulator vm)
    {
        public bool TryGetAsyncDeviceContext(int id, [NotNullWhen(true)] out AsyncDeviceContext? ctx)
        {
            ctx = vm.GetRawManagerDevices()
                .TryGetDevice<AsyncDevice>(id, out var device) 
                
                ? new AsyncDeviceContext(device) 
                : null;
            return ctx != null;
        }

        public AsyncDeviceContext? GetAsyncDeviceContext(int id)
        {
            return vm.GetRawManagerDevices()
                    .TryGetDevice<AsyncDevice>(id, out var device)
                ? new AsyncDeviceContext(device)
                : null;
        }

        public int RegisterAsyncDevice(DeviceSpec spec, uint sector)
        {
            var device = vm.GetRawManagerDevices()
                .CreateAsyncDevice(spec.RamSize, spec.ProcessorFaultPolicy);
            return vm.RegisterDevice(device, spec.Name, sector);
        }

        public IdState TryRegisterAsyncDevice(DeviceSpec spec,
            uint sector, out int id)
        {
            var device = vm.GetRawManagerDevices()
                .CreateAsyncDevice(spec.RamSize, spec.ProcessorFaultPolicy);

            return vm.TryRegisterDevice(device, spec.Name, sector, out id);
        }
    }

    extension(ManagerDevices md) 
    {
        public AsyncDevice CreateAsyncDevice(
            RamSize size, 
            IProcessorPolicy processorFaultPolicy)
        {
            return new AsyncDevice(md.PortBus, size, processorFaultPolicy);
        }
    }

    extension(AsyncDeviceContext ctx)
    {
        // ===== Массивы =====

        public byte[] GetRamAsNewArray(nuint start, nuint length)
        {
            ctx.ThrowIfRunning("Cannot copy the RAM of a running device");
            return ctx.GetRamAsNewArrayCore(start, length);
        }

        // От start до конца
        public byte[] GetRamAsNewArray(nuint start)
        {
            ctx.ThrowIfRunning("Cannot copy the RAM of a running device");
            nuint end = ctx.NativeReadOnlyBuffer.Length;
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, end, nameof(start));
            return ctx.GetRamAsNewArrayCore(start, end - start);
        }

        public byte[] GetAllRamAsNewArray()
        {
            ctx.ThrowIfRunning("Cannot copy the RAM of a running device");
            nuint end = ctx.NativeReadOnlyBuffer.Length;
            return ctx.GetRamAsNewArrayCore(0, end);
        }

        // ===== Span / Memory / byte[] =====

        public void CopyRamTo(Span<byte> destination, nuint start)
        {
            ctx.ThrowIfRunning("Cannot copy the RAM of a running device");
            ctx.CopyRamToCore(destination, start);
        }

        // Явная длина — полезно, когда destination больше, чем нужно скопировать
        public void CopyRamTo(Span<byte> destination, nuint start, nuint length)
        {
            ctx.ThrowIfRunning("Cannot copy the RAM of a running device");
            ArgumentOutOfRangeException.ThrowIfGreaterThan(length, (nuint)destination.Length, nameof(length));
            ctx.CopyRamToCore(destination[..(int)length], start);
        }

        public void CopyRamTo(byte[] destination, int destinationIndex, nuint start, int length)
        {
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentOutOfRangeException.ThrowIfNegative(destinationIndex);
            ArgumentOutOfRangeException.ThrowIfNegative(length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(destinationIndex + length, destination.Length);
            ctx.CopyRamTo(destination.AsSpan(destinationIndex, length), start);
        }

        public void CopyRamTo(Memory<byte> destination, nuint start) =>
            ctx.CopyRamTo(destination.Span, start);

        // ===== Try* (без исключений) =====

        public bool TryCopyRamTo(Span<byte> destination, nuint start)
        {
            if (ctx.IsRunning) return false;

            IReadOnlyNativeBuffer buf = ctx.NativeReadOnlyBuffer;
            nuint bufLen = buf.Length;
            nuint len = (nuint)destination.Length;
            if (start > bufLen || len > bufLen - start) return false;

            ctx.CopyRamToCore(destination, start);
            return true;
        }

        // ===== Типизированные чтения =====

        public T ReadRam<T>(nuint byteOffset) where T : unmanaged
        {
            ctx.ThrowIfRunning("Cannot copy the RAM of a running device");

            Span<byte> buffer = stackalloc byte[System.Runtime.CompilerServices.Unsafe.SizeOf<T>()];
            ctx.CopyRamToCore(buffer, byteOffset);
            return MemoryMarshal.Read<T>(buffer);
        }

        public void CopyRamTo<T>(Span<T> destination, nuint byteOffset) where T : unmanaged
        {
            ctx.ThrowIfRunning("Cannot copy the RAM of a running device");
            ctx.CopyRamToCore(MemoryMarshal.AsBytes(destination), byteOffset);
        }

        // ===== Поток — для больших объёмов без аллокации одного огромного массива =====

        public void CopyRamTo(Stream stream, nuint start, nuint length, int bufferSize = 81920)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfLessThan(bufferSize, 1);

            ctx.ThrowIfRunning("Cannot copy the RAM of a running device");

            IReadOnlyNativeBuffer buf = ctx.NativeReadOnlyBuffer;
            nuint bufLen = buf.Length;
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, bufLen, nameof(start));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(length, bufLen - start, nameof(length));

            byte[] chunk = new byte[bufferSize];
            nuint offset = start;
            nuint remaining = length;

            while (remaining > 0)
            {
                int n = (int)Math.Min((nuint)chunk.Length, remaining);
                ctx.CopyRamToCore(chunk.AsSpan(0, n), offset);
                stream.Write(chunk, 0, n);
                offset += (nuint)n;
                remaining -= (nuint)n;
            }
        }

        // ===== Внутреннее =====

        private byte[] GetRamAsNewArrayCore(nuint start, nuint length)
        {
            IReadOnlyNativeBuffer buf = ctx.NativeReadOnlyBuffer;
            nuint bufLen = buf.Length;

            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, bufLen, nameof(start));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(length, bufLen - start, nameof(length));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(length, (nuint)int.MaxValue, nameof(length));

            byte[] result = new byte[(int)length];
            ctx.CopyRamToCore(result, start);
            return result;
        }

        private void CopyRamToCore(Span<byte> destination, nuint start)
        {
            IReadOnlyNativeBuffer buf = ctx.NativeReadOnlyBuffer;
            nuint bufLen = buf.Length;
            nuint len = (nuint)destination.Length;

            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, bufLen, nameof(start));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(len, bufLen - start, nameof(destination));

            for (int i = 0; i < destination.Length; i++)
                destination[i] = buf[(nuint)i + start];
        }

        private void ThrowIfRunning(string msg)
        {
            if (ctx.IsRunning) throw new InvalidOperationException(msg);
        }
    }
    
}
