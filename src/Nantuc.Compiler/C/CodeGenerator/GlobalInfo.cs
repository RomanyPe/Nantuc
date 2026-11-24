namespace Nantuc.Compiler.C.CodeGenerator;

public struct GlobalInfo(
    string type,
    bool isArray = false,
    bool isPointer = false,
    string? pointedType = null,
    int arraySize = 0,
    bool isConst = false,
    bool isPointedConst = false,
    bool isSigned = false)
{    public string Type = type;
    public bool IsArray = isArray;
    public bool IsPointer = isPointer;
    public string? PointedType = pointedType;
    public int ArraySize = arraySize;
    public bool IsConst = isConst;
    public bool IsPointedConst = isPointedConst;
    public bool IsSigned = isSigned;
}
