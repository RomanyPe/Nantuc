namespace Nantuc.Compiler.C.CodeGenerator;

public struct GlobalInfo(ulong address,
                         string type,
                         bool isArray = false,
                         bool isPointer = false,
                         string? pointedType = null,
                         int arraySize = 0)
{
    public ulong Address = address;
    public string Type = type;
    public bool IsArray = isArray;
    public bool IsPointer = isPointer;
    public string? PointedType = pointedType;
    public int ArraySize = arraySize;
}
