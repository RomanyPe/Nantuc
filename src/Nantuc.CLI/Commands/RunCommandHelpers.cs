using Nantuc.Common;

namespace Nantuc.CLI.Commands
{
    internal static class RunCommandHelpers
    {
        public static string ToDisplayString(BiosStatus status) => status switch
        {
            BiosStatus.Success => "Success",
            BiosStatus.SegmentationFault => "SegmentationFault",
            BiosStatus.AlignmentFault => "AlignmentFault",
            BiosStatus.EndProgram => "EndProgram",
            BiosStatus.HostStopped => "HostStopped",
            BiosStatus.NotImplementedOpCode => "NotImplementedOpCode",
            BiosStatus.NullDeviceInput => "NullDeviceInput",
            BiosStatus.NullDeviceOutput => "NullDeviceOutput",
            BiosStatus.InfinityLoop => "InfinityLoop",
            BiosStatus.DivOnZero => "DivOnZero",
            BiosStatus.NotSupportedOpCode => "NotSupportedOpCode",
            BiosStatus.StackUnderflow => "StackUnderflow",
            BiosStatus.PauseSystem => "PauseSystem",

            _ => ((uint)status).ToString(),
        };

        public static bool TryParseDevicePortSize(string text, out DevicePortSize size)
        {
            switch (text.Trim().ToUpperInvariant().Replace(" ", ""))
            {
                case "8B": size = DevicePortSize.B8; return true;
                case "16B": size = DevicePortSize.B16; return true;
                case "32B": size = DevicePortSize.B32; return true;
                case "64B": size = DevicePortSize.B64; return true;
                case "128B": size = DevicePortSize.B128; return true;
                case "256B": size = DevicePortSize.B256; return true;

                default:
                    size = default;
                    return false;
            }
        }

        public static bool TryParsePortSize(string text, out PortSize size)
        {
            switch (text.Trim().ToUpperInvariant().Replace(" ", ""))
            {
                case "64B": size = PortSize.B64; return true;
                case "128B": size = PortSize.B128; return true;
                case "256B": size = PortSize.B256; return true;
                case "512B": size = PortSize.B512; return true;

                case "1KB": size = PortSize.KB1; return true;
                case "2KB": size = PortSize.KB2; return true;
                case "4KB": size = PortSize.KB4; return true;
                case "8KB": size = PortSize.KB8; return true;
                case "16KB": size = PortSize.KB16; return true;
                case "32KB": size = PortSize.KB32; return true;
                case "64KB": size = PortSize.KB64; return true;
                case "128KB": size = PortSize.KB128; return true;
                case "256KB": size = PortSize.KB256; return true;
                case "512KB": size = PortSize.KB512; return true;

                default:
                    size = default;
                    return false;
            }
        }

        public static bool TryParseRamSize(string text, out RamSize size)
        {
            switch (text.Trim().ToUpperInvariant().Replace(" ", ""))
            {
                case "64B": size = RamSize.B64; return true;
                case "128B": size = RamSize.B128; return true;
                case "256B": size = RamSize.B256; return true;
                case "512B": size = RamSize.B512; return true;

                case "1KB": size = RamSize.KB1; return true;
                case "2KB": size = RamSize.KB2; return true;
                case "4KB": size = RamSize.KB4; return true;
                case "8KB": size = RamSize.KB8; return true;
                case "16KB": size = RamSize.KB16; return true;
                case "32KB": size = RamSize.KB32; return true;
                case "64KB": size = RamSize.KB64; return true;
                case "128KB": size = RamSize.KB128; return true;
                case "256KB": size = RamSize.KB256; return true;
                case "512KB": size = RamSize.KB512; return true;

                case "1MB": size = RamSize.MB1; return true;
                case "2MB": size = RamSize.MB2; return true;
                case "4MB": size = RamSize.MB4; return true;
                case "8MB": size = RamSize.MB8; return true;
                case "16MB": size = RamSize.MB16; return true;
                case "32MB": size = RamSize.MB32; return true;
                case "64MB": size = RamSize.MB64; return true;
                case "128MB": size = RamSize.MB128; return true;
                case "256MB": size = RamSize.MB256; return true;
                case "512MB": size = RamSize.MB512; return true;

                case "1GB": size = RamSize.GB1; return true;
                case "2GB": size = RamSize.GB2; return true;

                default:
                    size = default;
                    return false;
            }
        }
    }
}