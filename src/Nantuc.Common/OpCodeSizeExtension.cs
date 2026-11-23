namespace Nantuc.Common;

public static class OpCodeSizeExtension
{
    public const int LastValueInt = (int)LastValue;
    public const OpCodeSize LastValue = OpCodeSize.S64;

    extension(OpCodeSize size)
    {
        public uint ToUint() => (uint)size;
        public string SizeName() => size switch
        {
            OpCodeSize.S8 => nameof(OpCodeSize.S8),
            OpCodeSize.S16 => nameof(OpCodeSize.S16),
            OpCodeSize.S32 => nameof(OpCodeSize.S32),
            OpCodeSize.S64 => nameof(OpCodeSize.S64),
            _ => "???"
        };
    }

}
