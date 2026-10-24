namespace Nantuc.Compiler.ASM.Base;

public abstract class AssemblerBase(ulong baseAddress)
{
    protected readonly ulong _baseAddress = baseAddress;
    public ulong BaseAddress => _baseAddress;
    public int CodeSectionSize { get; protected set; } = -1;
    public int DataSectionStart { get; protected set; } = -1;
    public int DataSectionSize { get; protected set; } = -1;

    public abstract byte[] Build();
    public abstract void EmitInstruction(uint instruction);
    public abstract void EmitInstruction64(uint instruction, ulong data);
    public abstract void EmitInstruction64WithLabel(uint instruction, string label);
    public abstract void EmitJump(uint jmpOpcode, string label);
    public abstract void EmitJumpToAbsolute(uint jmpOpcode, ulong absoluteTarget);
    public abstract void MarkLabel(string name);
    public abstract void EmitData(string v, byte[] data);
    
}