namespace Nantuc.Kernel.Common;

public readonly struct DecodeInstructionResult(OpCode opCode,
                                               RegType reg1,
                                               RegType reg2,
                                               OpCodeSize dataSizeCode)
{
    public readonly OpCode OpCode = opCode;
    public readonly RegType Reg1 = reg1;
    public readonly RegType Reg2 = reg2;
    public readonly OpCodeSize DataSizeCode = dataSizeCode;
}