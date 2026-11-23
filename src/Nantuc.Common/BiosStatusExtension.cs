namespace Nantuc.Common;

public static class BiosStatusExtension
{
    public const int LastValueInt = (int)LastValue;
    public const BiosStatus LastValue = BiosStatus.UnsafeOpCodeForbidden;

    extension(BiosStatus status)
    {
        public string AsText => status switch
        {
            BiosStatus.Success => nameof(BiosStatus.Success),
            BiosStatus.SegmentationFault => nameof(BiosStatus.SegmentationFault),
            BiosStatus.AlignmentFault => nameof(BiosStatus.AlignmentFault),
            BiosStatus.EndProgram => nameof(BiosStatus.EndProgram),
            BiosStatus.HostStopped => nameof(BiosStatus.HostStopped),
            BiosStatus.NotImplementedOpCode => nameof(BiosStatus.NotImplementedOpCode),
            BiosStatus.NullDeviceInput => nameof(BiosStatus.NullDeviceInput),
            BiosStatus.NullDeviceOutput => nameof(BiosStatus.NullDeviceOutput),
            BiosStatus.InfinityLoop => nameof(BiosStatus.InfinityLoop),
            BiosStatus.DivOnZero => nameof(BiosStatus.DivOnZero),
            BiosStatus.NotSupportedOpCode => nameof(BiosStatus.NotSupportedOpCode),
            BiosStatus.StackUnderflow => nameof(BiosStatus.StackUnderflow),
            BiosStatus.UnsafeOpCodeForbidden => nameof(BiosStatus.UnsafeOpCodeForbidden),

            _ => ((uint)status).ToString(),
        };
    }
}
