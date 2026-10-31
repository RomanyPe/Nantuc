using Nantuc.Compiler.ASM.Base;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C.AST;
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Diagnostics;


namespace Nantuc.Compiler.C.CodeGenerator;

// ============================================================
// FunctionGenerator – генерация программы и функций
// ============================================================
public class FunctionGenerator(AssemblerBase asm, Dictionary<string, StructLayout> structTable, ThrowCompiler ThrowCompiler)
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
            _globalMem.Allocate(global.Name, global.Type,
                                global.IsArray, global.IsPointer, global.PointedType,
                                global.ArraySize,
                                global.IsConst, global.IsPointedConst);
        }

        _functionTable.Clear();
        foreach (var func in program.FunctionNodes)
            _functionTable[func.Name] = func;

        _asm.MarkLabel("__global_init");

        GenerateGlobalInit(program);

        if (program.FunctionNodes.Any(f => f.Name == "main"))
        {
            _asm.EmitJmp("func_main");
        }

        foreach (var func in program.FunctionNodes)
        {
            if (func.IsExternal) continue;
            GenerateFunction(func);
        }

        foreach (var global in program.GlobalVarNodes)
        {
            int size = GetGlobalDataSize(global, _structTable);
             
            byte[] data = new byte[(size + 7) & ~7];

            if (global.ArrayInitializer != null && !global.IsPointer)
                FillArrayInitializerData(data, global, _structTable);
            // else — нули, реальные значения в __global_init
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

    private int GetGlobalDataSize(VariableNode varNode, Dictionary<string, StructLayout> structTable)
    {
        int baseSize;
        if (varNode.IsPointer)
            baseSize = 8;
        else if (structTable.TryGetValue(varNode.Type, out var layout))
            baseSize = layout.Size;
        else
            baseSize = CodeGenUtils.GetSizeInBytes(CodeGenUtils.GetSizeForType(varNode.Type, ThrowCompiler), ThrowCompiler);

        return varNode.IsArray ? baseSize * varNode.ArraySize : baseSize;
    }

    private void FillArrayInitializerData(byte[] data, VariableNode varNode,
                                                  Dictionary<string, StructLayout> structTable)
    {
        OpCodeSize elemSize = CodeGenUtils.GetSizeForType(varNode.Type, ThrowCompiler);
        int elemBytes = CodeGenUtils.GetSizeInBytes(elemSize, ThrowCompiler);
        int count = Math.Min(varNode.ArrayInitializer!.Count, varNode.ArraySize);

        for (int i = 0; i < count; i++)
        {
            long value = EvaluateConstant(varNode.ArrayInitializer[i], structTable);
            int offset = i * elemBytes;
            // little-endian запись
            for (int b = 0; b < elemBytes; b++)
                data[offset + b] = (byte)(value >> (b * 8));
        }

    }
    private long EvaluateConstant(ASTNode node, Dictionary<string, StructLayout> structTable) => node switch
    {
        NumberNode num => num.Value,
        UnaryOpNode { Operator: "-" } u => -EvaluateConstant(u.Operand, structTable),
        UnaryOpNode { Operator: "+" } u => EvaluateConstant(u.Operand, structTable),
        UnaryOpNode { Operator: "~" } u => ~EvaluateConstant(u.Operand, structTable),
        BinaryOpNode bin => ExpressionGenerator.ComputeConstant(
                                       EvaluateConstant(bin.Left, structTable),
                                       EvaluateConstant(bin.Right, structTable),
                                       bin.Operator, ThrowCompiler),
        SizeofTypeNode st => GetSizeofTypeConstant(st, structTable),
        _ => ThrowCompiler.Throw<long>(ErrorCode.NotSupported,
                 $"Non-constant initializer in array: {node.NameNode}")
    }; 
    
    private long GetSizeofTypeConstant(SizeofTypeNode st, Dictionary<string, StructLayout> structTable)
    {
        if (st.IsPointer) return 8;
        if (structTable.TryGetValue(st.TypeName, out var layout)) return layout.Size;
        if (CodeGenUtils.IsPrimitiveType(st.TypeName))
            return CodeGenUtils.GetSizeInBytes(CodeGenUtils.GetSizeForType(st.TypeName, ThrowCompiler), ThrowCompiler);
        ThrowCompiler.Throw(ErrorCode.CodeGen_UnknownTypeSize, st.TypeName);
        return 0;
    }

    private long EvaluateConstant(ASTNode node) => node switch
    {
        NumberNode num => num.Value,
        UnaryOpNode { Operator: "-" } u => -EvaluateConstant(u.Operand),
        UnaryOpNode { Operator: "+" } u => EvaluateConstant(u.Operand),
        UnaryOpNode { Operator: "~" } u => ~EvaluateConstant(u.Operand),
        BinaryOpNode bin => ExpressionGenerator.ComputeConstant(
                                                       EvaluateConstant(bin.Left),
                                                       EvaluateConstant(bin.Right),
                                                       bin.Operator, ThrowCompiler),
        _ => ThrowCompiler.Throw<long>(ErrorCode.NotSupported,
                 $"Non-constant initializer in array: {node.NameNode}")
    };
    private void GenerateGlobalInit(ProgramNode program)
    {
        // Временный контекст для вычисления глобальных инициализаторов
        var dummyCtx = new FunctionContext(); // не используется для varMap
        var getLabel = GetLabel;
        var exprGen = new ExpressionGenerator(_asm, _globalMem, dummyCtx, _functionTable, getLabel, _structTable, new VariableAccessor(_asm, dummyCtx, _globalMem));

        foreach (var global in program.GlobalVarNodes)
        {
            if (global.ArrayInitializer != null && global.IsPointer)
            {
                OpCodeSize elemSize = OpCodeSize.S64;
                int elemBytes = 8;
                for (int i = 0; i < global.ArrayInitializer.Count; i++)
                {
                    exprGen.GenerateExpression(global.ArrayInitializer[i]);   // → r0
                    _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN,
                                 GlobalMemoryManager.GetLabel(global.Name));
                    _asm.EmitLdi(RegType.r1, (ulong)(i * elemBytes));
                    _asm.EmitAdd(CodeGenUtils.ADDR_SCRATCH_REG_EN, RegType.r1);
                    _asm.EmitStoreInd(RegType.r0, CodeGenUtils.ADDR_SCRATCH_REG_EN, elemSize);
                }
            }
            if (global.Initializer != null)
            {
                // Пропускаем структуры – их инициализация пока не поддерживается
                if (CodeGenUtils.IsStructType(global.Type, _structTable))
                    continue;

                exprGen.GenerateExpression(global.Initializer);
                OpCodeSize opSize = global.IsPointer ? OpCodeSize.S64 : CodeGenUtils.GetSizeForType(global.Type, ThrowCompiler);
                string label = GlobalMemoryManager.GetLabel(global.Name);
                _asm.EmitStore(RegType.r0, opSize, label);
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
        var funcCtx = FunctionContext.Create(func, localVarNodes, _structTable, ThrowCompiler);
        var varAccessor = new VariableAccessor(_asm, funcCtx, _globalMem);
        // Генераторы для тела функции
        var getLabel = GetLabel;
        var exprGen = new ExpressionGenerator(_asm, _globalMem, funcCtx, _functionTable, getLabel, _structTable, varAccessor);
        var stmtGen = new StatementGenerator(_asm, exprGen, funcCtx, getLabel, _globalMem, _structTable, varAccessor);

        bool isMain = func.Name == "main";
        int stackAlloc = funcCtx.TotalLocalSize + funcCtx.SaveAreaSize;
        var savedRegs = funcCtx.UsedRegisters
           .OrderBy(r => (int)r)
           .ToList();

        if (!isMain)
        {
            foreach (var reg in savedRegs)
                _asm.EmitPush(reg);
        }

        // Пролог
        if (stackAlloc > 0)
        {
            _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)stackAlloc);
            _asm.EmitSub(RegType.rSP, CodeGenUtils.ADDR_SCRATCH_REG_EN);
        }

        if (!isMain) 
        {
            for (int i = 0; i < func.Parameters.Count; i++)
            {
                var param = func.Parameters[i];
                if (i < 4)
                {
                    RegType incomingReg = (RegType)i;
                    if (!funcCtx.VarMap.TryGetValue(param.Name, out var loc))
                        ThrowCompiler.Throw(ErrorCode.CodeGen_ParameterNotInRegister, param.Name);
                    if (loc.IsRegister)
                    {
                        if (loc.Register != incomingReg)
                            _asm.EmitMov(loc.Register, incomingReg);
                    }
                    else
                    {
                        _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)loc.StackOffset);
                        _asm.EmitAdd(CodeGenUtils.ADDR_SCRATCH_REG_EN, RegType.rSP);
                        _asm.EmitStoreInd(incomingReg, CodeGenUtils.ADDR_SCRATCH_REG_EN, loc.TypeSize);
                    }
                }
                else
                {
                    if (!funcCtx.VarMap.TryGetValue(param.Name, out var loc) || !loc.IsRegister)
                        ThrowCompiler.Throw(ErrorCode.CodeGen_ParameterNotInRegister, param.Name);
                    string globalName = $"__param_{func.Name}_{param.Name}";
                    string label = GlobalMemoryManager.GetLabel(globalName);
                    OpCodeSize size = param.IsPointer ? OpCodeSize.S64 : CodeGenUtils.GetSizeForType(param.Type, ThrowCompiler);
                    _asm.EmitLoad(loc.Register, size, label);
                }
            }        
        }
        stmtGen.GenerateBlock(func.Body);
        // Эпилог
        if (isMain)
        {
            _asm.EmitEnd();
        }
        else
        {
            _asm.MarkLabel(funcCtx.EpilogueLabel);
            if (stackAlloc > 0)
            {
                _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)stackAlloc);
                _asm.EmitAdd(RegType.rSP, CodeGenUtils.ADDR_SCRATCH_REG_EN);
            }

            for (int i = savedRegs.Count - 1; i >= 0; i--)
                _asm.EmitPop(savedRegs[i]);

            _asm.EmitRet();
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
                        else if (fr.Init is BlockNode initBlock)
                            foreach (var s in initBlock.Statements)
                                if (s is VariableNode v) map[v.Name] = v;
                        CollectLocalVars(fr.Body, map);
                        break;
                    }
                case DoWhileNode dw:
                    if (dw.Body is BlockNode bodyBlock) CollectLocalVars(bodyBlock, map);
                    break;
            }
        }
    }

    private string GetLabel() => $"L{_labelCounter++}";
}