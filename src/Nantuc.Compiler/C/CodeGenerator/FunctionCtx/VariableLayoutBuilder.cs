using Nantuc.Common;
using Nantuc.Compiler.C.CodeGenerator.RegistersAlloc;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.CodeGenerator.FunctionCtx;

internal static class VariableLayoutBuilder
{
    public readonly record struct VarInfo(
        string Name, string Type, bool IsArray, int ArraySize,
        bool IsPointer, string? PointedType, bool IsConst, bool IsPointedConst);

    public static VarLocation Build(
        VarInfo v,
        int stackOffset,
        RegisterAllocationResult allocation,
        Dictionary<string, StructLayout> structTable,
        ErrorReporter er,
        out int consumedBytes)
    {
        var (varSize, elementSize) = ComputeVarSize(v, structTable, er);
        bool isSigned = ComputeIsSigned(v, structTable);

        if (CanUseRegister(v, structTable)
            && allocation.VarToRegister.TryGetValue(v.Name, out var reg)
            && reg.HasValue)
        {
            consumedBytes = 0;
            return MakeRegisterLocation(v, reg.Value, isSigned, er);
        }

        consumedBytes = varSize;
        return MakeStackLocation(v, stackOffset, elementSize, isSigned, structTable);
    }

    private static (int varSize, OpCodeSize elementSize) ComputeVarSize(
        VarInfo v, Dictionary<string, StructLayout> structTable, ErrorReporter er)
    {
        StructLayout? layout = null;
        bool isStruct = !v.IsPointer && structTable.TryGetValue(v.Type, out layout);

        int varSize;
        OpCodeSize elementSize = OpCodeSize.S64;

        if (v.IsArray && v.IsPointer) varSize = 8 * v.ArraySize;
        else if (v.IsPointer) varSize = 8;
        else if (isStruct) varSize = layout!.Size;
        else
        {
            elementSize = CodeGenUtils.GetSizeForType(v.Type, er);
            varSize = v.IsArray
                ? CodeGenUtils.GetSizeInBytes(elementSize, er) * v.ArraySize
                : 8;
        }

        if (v.IsArray && isStruct)
            varSize = layout!.Size * v.ArraySize;

        varSize = varSize.AlignUp(8);
        return (varSize, elementSize);
    }

    private static bool CanUseRegister(VarInfo v, Dictionary<string, StructLayout> t)
        => !v.IsArray && !v.IsPointer && !t.ContainsKey(v.Type);

    private static bool ComputeIsSigned(VarInfo v, Dictionary<string, StructLayout> t)
        => !v.IsPointer && !t.ContainsKey(v.Type) && CodeGenUtils.IsSignedType(v.Type);

    private static VarLocation MakeRegisterLocation(
        VarInfo v, RegType reg, bool isSigned, ErrorReporter er) => new()
        {
            IsRegister = true,
            Register = reg,
            TypeSize = CodeGenUtils.GetSizeForType(v.Type, er),
            IsArray = false,
            ArraySize = 0,
            IsPointer = v.IsPointer,
            PointedType = v.PointedType,
            IsConst = v.IsConst,
            IsPointedConst = v.IsPointedConst,
            IsSigned = isSigned,
        };

    private static VarLocation MakeStackLocation(
        VarInfo v, int stackOffset, OpCodeSize size, bool isSigned,
        Dictionary<string, StructLayout> t) => new()
        {
            IsRegister = false,
            StackOffset = stackOffset,
            TypeSize = size,
            StructTypeName = t.ContainsKey(v.Type) ? v.Type : null,
            IsArray = v.IsArray,
            ArraySize = v.ArraySize,
            IsPointer = v.IsPointer,
            PointedType = v.PointedType,
            IsConst = v.IsConst,
            IsPointedConst = v.IsPointedConst,
            IsSigned = isSigned,
        };
}
