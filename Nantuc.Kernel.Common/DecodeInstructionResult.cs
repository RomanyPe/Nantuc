namespace Nantuc.Kernel.Common;

/// <summary>
/// Результат декодирования всей инструкции
/// </summary>
/// <param name="opCode">код операции</param>
/// <param name="reg1">первый регистр </param>
/// <param name="reg2">второй регистр</param>
/// <param name="dataSizeCode">параеметр размера</param>
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