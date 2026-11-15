using Nantuc.Common;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C.AST;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.CodeGenerator.FunctionGen;

public partial class FunctionGenerator
{
    // ============================================================
    // Секция данных: глобальные переменные + pseudo-param слоты
    // для параметров 4+.
    // ============================================================
    private void EmitDataSection(ProgramNode program)
    {
        EmitGlobalVariablesData(program);
        EmitExtraParameterSlots(program);
    }

    private void EmitGlobalVariablesData(ProgramNode program)
    {
        foreach (var g in program.GlobalVarNodes)
        {
            int size = GetGlobalDataSize(g);
            byte[] data = new byte[(size + 7) & ~7];

            if (g.ArrayInitializer != null && !g.IsPointer)
                FillArrayInitializerData(data, g);

            // Иначе — нули; реальные значения пишет GenerateGlobalInit.
            _asm.EmitData(GlobalMemoryManager.GetLabel(g.Name), data);
        }
    }

    private void EmitExtraParameterSlots(ProgramNode program)
    {
        foreach (var func in program.FunctionNodes)
        {
            for (int i = 4; i < func.Parameters.Count; i++)
            {
                var param = func.Parameters[i];
                string globalName = $"__param_{func.Name}_{param.Name}";
                _asm.EmitData(GlobalMemoryManager.GetLabel(globalName), new byte[8]);
            }
        }
    }

    private int GetGlobalDataSize(VariableNode varNode)
    {
        int baseSize;
        if (varNode.IsPointer)
            baseSize = 8;
        else if (_structTable.TryGetValue(varNode.Type, out var layout))
            baseSize = layout.Size;
        else
            baseSize = CodeGenUtils.GetSizeInBytes(
                CodeGenUtils.GetSizeForType(varNode.Type, _erRep), _erRep);

        return varNode.IsArray ? baseSize * varNode.ArraySize : baseSize;
    }

    private void FillArrayInitializerData(byte[] data, VariableNode varNode)
    {
        OpCodeSize elemSize = CodeGenUtils.GetSizeForType(varNode.Type, _erRep);
        int elemBytes = CodeGenUtils.GetSizeInBytes(elemSize, _erRep);
        int count = Math.Min(varNode.ArrayInitializer!.Count, varNode.ArraySize);

        for (int i = 0; i < count; i++)
        {
            long value = ConstantEvaluator.Evaluate(varNode.ArrayInitializer[i], _structTable, _erRep);
            int offset = i * elemBytes;

            // little-endian запись
            for (int b = 0; b < elemBytes; b++)
                data[offset + b] = (byte)(value >> (b * 8));
        }
    }
}