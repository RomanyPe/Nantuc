using Nantuc.Kernel.Contracts;
using Nantuc.VmApplication.Emulator;

namespace Nantuc.VmApplication.Extensions.DeviceCtx;

public static class DeviceContextExtensions
{
    extension(DeviceContext ctx)
    {
        public byte[] GetRamAsNewArray(nuint start, nuint length)
        {
            ctx.ThrowIfRunning("Cannot read the RAM of a running device");

            INativeReadOnlyBuffer innerBuffer = ctx.NativeReadOnlyBuffer;
            nuint bufferLength = innerBuffer.Length;

            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, bufferLength, nameof(start));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(length, bufferLength - start, nameof(length));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(length, (nuint)int.MaxValue, nameof(length));

            int len = (int)length;
            byte[] resultBuffer = new byte[len];
            for (int i = 0; i < len; i++)
                resultBuffer[i] = innerBuffer[(nuint)i + start];
            return resultBuffer;
        }

        public void CopyRamTo(Span<byte> destination, nuint start)
        {
            ctx.ThrowIfRunning("Cannot read the RAM of a running device");

            INativeReadOnlyBuffer innerBuffer = ctx.NativeReadOnlyBuffer;
            nuint bufferLength = innerBuffer.Length;
            nuint length = (nuint)destination.Length;

            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, bufferLength, nameof(start));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(length, bufferLength - start, nameof(destination));

            for (int i = 0; i < destination.Length; i++)
                destination[i] = innerBuffer[(nuint)i + start];
        }

        private void ThrowIfRunning(string msg)
        {
            if (ctx.IsRunning) throw new InvalidOperationException(msg);
        }
    }
}