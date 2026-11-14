namespace Nantuc.Compiler.ASM.IR;

public sealed class AsmLabel(string name) : AsmItem
{
    public string Name { get; } = name;
}

