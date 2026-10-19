using Nantuc.Compiler.ASM;
using Nantuc.Compiler.ASM.Optimizators;
using Nantuc.Compiler.C;
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Diagnostics;


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

        //foreach (var s in program.Structs)
        //{
        //    _structTable[s.Name] = new(s.Name, s.Fields, _structTable);
        //}

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
            byte[] data = new byte[(size + 7) & ~7]; // выравнивание до 8
            _asm.EmitData(GlobalMemoryManager.GetLabel(global.Name), data);
        }

        foreach (var func in program.FunctionNodes)
        {
            for (int i = 4; i < func.Parameters.Count; i++)
            {
                var param = func.Parameters[i];
                string globalName = $"__param_{func.Name}_{param.Name}";
                // размер всегда 8 (для простоты)
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
        if (!isMain)
        {
            foreach (var reg in funcCtx.UsedRegisters.OrderBy(r => (int)r))
                _asm.EmitInstruction(InstructionEncoder.EncodeU(OpCode.PUSH.ToUint(), (uint)reg));

            // Вместо загрузки из псевдоглобалов:
            for (int i = 0; i < func.Parameters.Count; i++)
            {
                var param = func.Parameters[i];
                if (i < 4)
                {
                    // Параметр пришёл в регистре r0..r3
                    RegType incomingReg = (RegType)i;
                    if (!funcCtx.VarMap.TryGetValue(param.Name, out var loc))
                        ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_ParameterNotInRegister, param.Name);

                    if (loc.IsRegister)
                    {
                        // Перемещаем в выделенный регистр
                        if (loc.Register != incomingReg)
                            _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.MOV.ToUint(), loc.Register.ToUint(), incomingReg.ToUint()));
                    }
                    else
                    {
                        // Сохраняем в стек
                        _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(CodeGenUtils.TMP_REG), (ulong)loc.StackOffset);
                        _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), CodeGenUtils.TMP_REG, RegType.rSP.ToUint()));
                        _asm.EmitInstruction(InstructionEncoder.EncodeSTORE_IND(incomingReg.ToUint(), CodeGenUtils.TMP_REG, loc.TypeSize.ToUint()));
                    }
                }
                else
                {
                    // Для параметров > 4 оставляем старый механизм (псевдоглобалы)
                    if (!funcCtx.VarMap.TryGetValue(param.Name, out var loc) || !loc.IsRegister)
                        ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_ParameterNotInRegister, param.Name);
                    string globalName = $"__param_{func.Name}_{param.Name}";
                    var addr = _globalMem.GetInfo(globalName)!.Value.Address;
                    OpCodeSize size = param.IsPointer ? OpCodeSize.S64 : CodeGenUtils.GetSizeForType(param.Type);
                    _asm.EmitInstruction64(InstructionEncoder.EncodeLOAD(loc.Register.ToUint(), size.ToUint()), addr);
                }
            }

            if (funcCtx.TotalLocalSize > 0)
            {
                _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(CodeGenUtils.TMP_REG), (ulong)funcCtx.TotalLocalSize);
                _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.SUB.ToUint(), (uint)RegType.rSP, CodeGenUtils.TMP_REG));
            }
        }
        else
        {
            if (funcCtx.TotalLocalSize > 0)
            {
                _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(CodeGenUtils.TMP_REG), (ulong)funcCtx.TotalLocalSize);
                _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.SUB.ToUint(), (uint)RegType.rSP, CodeGenUtils.TMP_REG));
            }
        }

        // Тело
        stmtGen.GenerateBlock(func.Body);

        // Эпилог
        if (isMain)
        {
            _asm.EmitInstruction(InstructionEncoder.EncodeEND());
        }
        else
        {
            _asm.MarkLabel(funcCtx.EpilogueLabel);
            if (funcCtx.TotalLocalSize > 0)
            {
                _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(CodeGenUtils.TMP_REG), (ulong)funcCtx.TotalLocalSize);
                _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), (uint)RegType.rSP, CodeGenUtils.TMP_REG));
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