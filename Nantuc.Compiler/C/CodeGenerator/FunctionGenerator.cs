using Nantuc.Compiler.ASM;
using Nantuc.Compiler.ASM.Optimizators;
using Nantuc.Compiler.C;
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Diagnostics;
using System.Buffers;
using System.Drawing;


namespace Nantuc.Compiler.C.CodeGenerator;

// ============================================================
// FunctionGenerator – генерация программы и функций
// ============================================================
public class FunctionGenerator(AssemblerBase asm, Dictionary<string, StructLayout> structTable)
{
    private readonly AssemblerBase _asm = asm;
    private GlobalMemoryManager _globalMem = null!;
    private readonly Dictionary<string, FunctionNode> _functionTable = [];
    private readonly Dictionary<string, StructLayout> _structTable = structTable;
    private int _labelCounter;

    public void Generate(ProgramNode program)
    {
        _globalMem = new GlobalMemoryManager();

        foreach (var global in program.GlobalVarNodes)
        {
            _globalMem.Allocate(global.Name, global.Type, global.IsArray, global.IsPointer, global.PointedType, global.ArraySize);
        }


        _functionTable.Clear();
        foreach (var func in program.FunctionNodes)
            _functionTable[func.Name] = func;

        GenerateGlobalInit(program);

        foreach (var func in program.FunctionNodes)
        {
            if (func.IsExternal) continue;
            GenerateFunction(func);
        }

        foreach (var global in program.GlobalVarNodes)
        {
            int size = GetGlobalDataSize(global, _structTable);
             
            byte[] data = new byte[(size + 7) & ~7];

            if (global.ArrayInitializer != null)
                FillArrayInitializerData(data, global);

            _asm.EmitData(GlobalMemoryManager.GetLabel(global.Name), data);
        }

        foreach (var func in program.FunctionNodes)
        {
            for (int i = 4; i < func.Parameters.Count; i++)
            {
                var param = func.Parameters[i];
                string globalName = $"__param_{func.Name}_{param.Name}";
                byte[] data = new byte[8];
                _asm.EmitData(GlobalMemoryManager.GetLabel(globalName), data);
            }
        }
    }

    private static int GetGlobalDataSize(VariableNode varNode, Dictionary<string, StructLayout> structTable)
    {
        int baseSize;
        if (varNode.IsPointer)
            baseSize = 8;
        else if (structTable.TryGetValue(varNode.Type, out var layout))
            baseSize = layout.Size;
        else
            baseSize = CodeGenUtils.GetSizeInBytes(CodeGenUtils.GetSizeForType(varNode.Type));

        return varNode.IsArray ? baseSize * varNode.ArraySize : baseSize;
    }

    private static void FillArrayInitializerData(byte[] data, VariableNode varNode)
    {
        OpCodeSize elemSize = CodeGenUtils.GetSizeForType(varNode.Type);
        int elemBytes = CodeGenUtils.GetSizeInBytes(elemSize);
        int count = Math.Min(varNode.ArrayInitializer!.Count, varNode.ArraySize);

        for (int i = 0; i < count; i++)
        {
            long value = EvaluateConstant(varNode.ArrayInitializer[i]);
            int offset = i * elemBytes;
            // little-endian запись
            for (int b = 0; b < elemBytes; b++)
                data[offset + b] = (byte)(value >> (b * 8));
        }
        // остаток (ArraySize > initializer.Count) и padding — нули (C-семантика)
    }

    private static long EvaluateConstant(ASTNode node) => node switch
    {
        NumberNode num => num.Value,
        UnaryOpNode { Operator: "-" } u => -EvaluateConstant(u.Operand),
        UnaryOpNode { Operator: "+" } u => EvaluateConstant(u.Operand),
        UnaryOpNode { Operator: "~" } u => ~EvaluateConstant(u.Operand),
        BinaryOpNode bin => ExpressionGenerator.ComputeConstant(
                                                       EvaluateConstant(bin.Left),
                                                       EvaluateConstant(bin.Right),
                                                       bin.Operator),
        _ => ThrowHelper.ThrowMiniC<long>(ErrorCode.NotSupported,
                 $"Non-constant initializer in array: {node.GetType().Name}")
    };
    private void GenerateGlobalInit(ProgramNode program)
    {
        // Временный контекст для вычисления глобальных инициализаторов
        var dummyCtx = new FunctionContext(); // не используется для varMap
        var getLabel = GetLabel;
        var exprGen = new ExpressionGenerator(_asm, _globalMem, dummyCtx, _functionTable, getLabel, _structTable, new VariableAccessor(_asm, dummyCtx, _globalMem));

        foreach (var global in program.GlobalVarNodes)
        {
            if (global.Initializer != null)
            {
                // Пропускаем структуры – их инициализация пока не поддерживается
                if (CodeGenUtils.IsStructType(global.Type, _structTable))
                    continue;

                exprGen.GenerateExpression(global.Initializer);
                OpCodeSize opSize = global.IsPointer ? OpCodeSize.S64 : CodeGenUtils.GetSizeForType(global.Type);
                string label = GlobalMemoryManager.GetLabel(global.Name);
                _asm.EmitInstruction64WithLabel(InstructionEncoder.EncodeSTORE(RegType.r1.ToUint(), opSize.ToUint()), label);
            }
        }
    }

    private void GenerateFunction(FunctionNode func)
    {
        _asm.MarkLabel($"func_{func.Name}");

        // Сбор локальных переменных
        var localVarNodes = new Dictionary<string, VariableNode>();
        CollectLocalVars(func.Body, localVarNodes);

        // Создание контекста функции
        var funcCtx = FunctionContext.Create(func, localVarNodes, _structTable);
        var varAccessor = new VariableAccessor(_asm, funcCtx, _globalMem);
        // Генераторы для тела функции
        var getLabel = GetLabel;
        var exprGen = new ExpressionGenerator(_asm, _globalMem, funcCtx, _functionTable, getLabel, _structTable, varAccessor);
        var stmtGen = new StatementGenerator(_asm, exprGen, funcCtx, getLabel, _globalMem, _structTable, varAccessor);

        bool isMain = func.Name == "main";

        // Пролог
        int stackAlloc = funcCtx.TotalLocalSize + funcCtx.SaveAreaSize;
        if (stackAlloc > 0)
        {
            _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(CodeGenUtils.TMP_REG), (ulong)stackAlloc);
            _asm.EmitInstruction(InstructionEncoder.EncodeR(
                OpCode.SUB.ToUint(), RegType.rSP.ToUint(), CodeGenUtils.TMP_REG));
        }
        else
        {
            if (funcCtx.TotalLocalSize > 0)
            {
                _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(CodeGenUtils.TMP_REG), (ulong)funcCtx.TotalLocalSize);
                _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.SUB.ToUint(), RegType.rSP.ToUint(), CodeGenUtils.TMP_REG));
            }
        }

        // Тело
        stmtGen.GenerateBlock(func.Body);

        // Эпилог
        if (stackAlloc > 0)
        {
            _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(CodeGenUtils.TMP_REG), (ulong)stackAlloc);
            _asm.EmitInstruction(InstructionEncoder.EncodeR(
                OpCode.ADD.ToUint(), RegType.rSP.ToUint(), CodeGenUtils.TMP_REG));
        }
        else
        {
            _asm.MarkLabel(funcCtx.EpilogueLabel);
            if (funcCtx.TotalLocalSize > 0)
            {
                _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(CodeGenUtils.TMP_REG), (ulong)funcCtx.TotalLocalSize);
                _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), RegType.rSP.ToUint(), CodeGenUtils.TMP_REG));
            }
            for (int i = funcCtx.UsedRegisters.Count - 1; i >= 0; i--)
                _asm.EmitInstruction(InstructionEncoder.EncodeU(OpCode.POP.ToUint(), (uint)funcCtx.UsedRegisters[i]));
            _asm.EmitInstruction(InstructionEncoder.EncodeRET());
        }
    }

    private static void CollectLocalVars(BlockNode block, Dictionary<string, VariableNode> map)
    {
        foreach (var stmt in block.Statements)
        {
            switch (stmt)
            {
                case VariableNode varNode:
                    map[varNode.Name] = varNode;
                    break;
                case BlockNode nested:
                    CollectLocalVars(nested, map);
                    break;
                case IfNode ifn:
                    CollectLocalVars(ifn.ThenBlock, map); if (ifn.ElseBlock != null) CollectLocalVars(ifn.ElseBlock, map); break;
                case WhileNode wh:
                    CollectLocalVars(wh.Body, map);
                    break;
                case ForNode fr:
                    {
                        if (fr.Init is VariableNode forVar)
                            map[forVar.Name] = forVar;
                        CollectLocalVars(fr.Body, map);
                        break;
                    }
            }
        }
    }

    private string GetLabel() => $"L{_labelCounter++}";
}