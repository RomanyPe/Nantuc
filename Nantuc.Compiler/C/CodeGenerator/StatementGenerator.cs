using Nantuc.Compiler.ASM;
using Nantuc.Compiler.ASM.Base;
using Nantuc.Compiler.ASM.IR;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Diagnostics;

namespace Nantuc.Compiler.C.CodeGenerator;

// ============================================================
// StatementGenerator – генерация инструкций
// ============================================================
public class StatementGenerator(AssemblerBase asm, ExpressionGenerator exprGen, FunctionContext funcCtx, Func<string> getLabel, GlobalMemoryManager globalMem, Dictionary<string, StructLayout> structTable, VariableAccessor varAccessor)
{
    private readonly GlobalMemoryManager _globalMem = globalMem;
    private readonly AssemblerBase _asm = asm;
    private readonly ExpressionGenerator _exprGen = exprGen;
    private readonly FunctionContext _funcCtx = funcCtx;
    private readonly Dictionary<string, StructLayout> _structTable = structTable;
    private readonly Func<string> _getLabel = getLabel;
    private readonly VariableAccessor _varAccessor = varAccessor;

    private readonly Stack<(string BreakLabel, string ContinueLabel)> _loopStack = [];

    public void GenerateBlock(BlockNode block)
    {
        foreach (var stmt in block.Statements)
        {
            switch (stmt)
            {
                case VariableNode var:
                    if (var.Initializer != null)
                    {
                        if (_funcCtx.VarMap.TryGetValue(var.Name, out var varLoc) && varLoc.IsRegister)
                        {
                            // Генерируем инициализатор сразу в регистр переменной
                            _exprGen.GenerateExpression(var.Initializer, varLoc.Register);
                        }
                        else
                        {
                            _exprGen.GenerateExpression(var.Initializer);
                            _varAccessor.StoreFromRegister(var.Name, RegType.r0);
                        }
                    }
                    else if (var.ArrayInitializer != null && var.IsArray)
                    {
                        GenerateLocalArrayInit(var);
                    }
                    break;
                case AssignmentNode assign:
                    GenerateAssignment(assign);
                    break;
                case BinaryOpNode binop:
                    _exprGen.GenerateExpression(binop);   // выражение как инструкция
                    break;
                case UnaryOpNode unop:
                    _exprGen.GenerateExpression(unop);
                    break;
                case IfNode ifNode:
                    GenerateIf(ifNode);
                    break;
                case WhileNode whileNode:
                    GenerateWhile(whileNode);
                    break;
                case ForNode forNode:
                    GenerateFor(forNode);
                    break;
                case ReturnNode ret:
                    GenerateReturn(ret);
                    break;
                case FunctionCallNode call:
                    _exprGen.GenerateExpression(call);
                    break;
                case InlineAsmNode asm:
                    GenerateInlineAsm(asm);
                    break;
                case DoWhileNode doWhile:
                    GenerateDoWhile(doWhile);
                    break;
                case ContinueNode:
                    GenerateContinue();
                    break;
                case BreakNode:
                    GenerateBreak();
                    break;
                case IncrementNode inc:
                    _exprGen.GenerateExpression(inc);   // результат отбрасывается
                    break;
            }
        }
    }
    private void GenerateLocalArrayInit(VariableNode var)
    {
        if (!_funcCtx.VarMap.TryGetValue(var.Name, out var loc) || loc.IsRegister)
            _asm.ThrowCompiler.Throw(ErrorCode.CodeGen_CannotGetRegisterAddress, var.Name);

        OpCodeSize elemSize = loc.TypeSize;
        int elemBytes = CodeGenUtils.GetSizeInBytes(elemSize, _asm.ThrowCompiler);
        int providedCount = Math.Min(var.ArrayInitializer!.Count, var.ArraySize);

        // 1. Явно заданные значения
        for (int i = 0; i < providedCount; i++)
        {
            _exprGen.GenerateExpression(var.ArrayInitializer[i]);
            EmitStoreToLocalArrayElement(loc.StackOffset + i * elemBytes, elemSize);
        }

        // 2. Хвост — нули (C-семантика: int y[10] = {5} ⇒ y[1..9] == 0)
        if (var.ArraySize > providedCount)
        {
            _asm.EmitLdi(RegType.r0, 0);
            for (int i = providedCount; i < var.ArraySize; i++)
                EmitStoreToLocalArrayElement(loc.StackOffset + i * elemBytes, elemSize);
        }
    }

    private void EmitStoreToLocalArrayElement(int offset, OpCodeSize elemSize)
    {
        _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)offset);
        _asm.EmitAdd(CodeGenUtils.ADDR_SCRATCH_REG_EN, RegType.rSP);
        _asm.EmitStoreInd(RegType.r0, CodeGenUtils.ADDR_SCRATCH_REG_EN, elemSize);
    }

    private void GenerateInlineAsm(InlineAsmNode asm)
    {
        var asmParser = new AssemblerParser();
        asmParser.Assemble(asm.AsmCode, _asm, allowDataSection: false);
    }

    private void GenerateAssignment(AssignmentNode assign)
    {
        // В начале метода, перед существующими ветками:
        if (assign.CompoundOp != null && assign.CompoundOp != "=")
        {
            GenerateCompoundAssignment(assign);
            return;
        }
        // 1) arr[i] = value
        if (assign.IndexExpr != null)
        {
            _exprGen.GenerateExpression(assign.Value);       // value → r1
            _exprGen.StoreR0ToArrayElement(assign.Name, assign.IndexExpr);  // внутри save
            return;
        }

        // 2) *ptr = value
        if (assign.LValue is DereferenceNode deref)
        {
            _exprGen.GenerateExpression(assign.Value);       // value → r1
            int slot = _exprGen.SaveRegToStack(RegType.r0);

            _exprGen.GenerateExpression(deref.Operand, RegType.r0);   // ptr → r1
            OpCodeSize size = GetPointedSize(deref.Operand);

            _exprGen.RestoreRegFromStack(slot, RegType.r1);           // value → r2
            _asm.EmitStoreInd(RegType.r1, RegType.r0, size);
            return;
        }

        // 3) obj.field = value (включая a[i].field = value, p->field = value)
        if (assign.LValue is MemberAccessNode memberAccess)
        {
            _exprGen.GenerateExpression(assign.Value);       // value → r1
            int slot = _exprGen.SaveRegToStack(RegType.r0);

            _exprGen.GenerateMemberAddress(memberAccess, RegType.r0);  // &field → r1
            OpCodeSize size = GetFieldSize(memberAccess);

            _exprGen.RestoreRegFromStack(slot, RegType.r1);            // value → r2
            _asm.EmitStoreInd(RegType.r1, RegType.r0, size);
            return;
        }

        // 4) x = <простое значение>  — оптимизация без save
        if (assign.IndexExpr == null && assign.LValue == null &&
            _funcCtx.VarMap.TryGetValue(assign.Name, out var regLoc) && regLoc.IsRegister &&
            (assign.Value is NumberNode || assign.Value is IdentifierNode))
        {
            _exprGen.GenerateExpression(assign.Value, regLoc.Register);
            return;
        }

        // 5) x = x op expr  — оптимизация без save
        if (_funcCtx.VarMap.TryGetValue(assign.Name, out var loc) && loc.IsRegister)
        {
            if (assign.Value is BinaryOpNode binop && binop.Left is IdentifierNode id && id.Name == assign.Name
                && (binop.Operator == "+" || binop.Operator == "-" || binop.Operator == "*" || binop.Operator == "/"))
            {
                RegType xReg = loc.Register;
                RegType tempReg = (xReg != RegType.r0) ? RegType.r0 : RegType.r1;
                _exprGen.GenerateExpression(binop.Right, tempReg);
                switch (binop.Operator)
                {
                    case "+": _asm.EmitAdd(xReg, tempReg); break;
                    case "-": _asm.EmitSub(xReg, tempReg); break;
                    case "*": _asm.EmitMultInt(xReg, tempReg); break;
                    case "/": _asm.EmitDiv(xReg, tempReg); break;
                }
                return;
            }

            _exprGen.GenerateExpression(assign.Value, loc.Register);
            return;
        }

        // 6) Общий случай: x (в стеке/глобале) = value
        _exprGen.GenerateExpression(assign.Value);
        _varAccessor.StoreFromRegister(assign.Name, RegType.r0);
    }

    private void GenerateCompoundAssignment(AssignmentNode assign)
    {
        // CompoundOp гарантированно не null и не "=" (проверено в GenerateAssignment)
        string baseOp = assign.CompoundOp!.TrimEnd('=');   // "+=" → "+", "<<=" → "<<"

        // 1) arr[i] op= value
        if (assign.IndexExpr != null)
        {
            GenerateCompoundArrayElement(assign.Name, assign.IndexExpr, baseOp, assign.Value);
            return;
        }

        // 2) *ptr op= value
        if (assign.LValue is DereferenceNode deref)
        {
            GenerateCompoundDeref(deref, baseOp, assign.Value);
            return;
        }

        // 3) obj.field op= value (включая arr[i].f, p->f)
        if (assign.LValue is MemberAccessNode member)
        {
            GenerateCompoundMember(member, baseOp, assign.Value);
            return;
        }

        // 4) x op= value
        GenerateCompoundIdent(assign.Name, baseOp, assign.Value);
    }

    private void GenerateCompoundIdent(string name, string baseOp, ASTNode rvalue)
    {
        // Быстрый путь: переменная уже в регистре (r5..r26 — не scratch, безопасно)
        if (_varAccessor.TryGetRegister(name, out RegType xReg))
        {
            RegType tempReg = (xReg != RegType.r0) ? RegType.r0 : RegType.r1;
            _exprGen.GenerateExpression(rvalue, tempReg);
            _exprGen.EmitBinaryOperation(baseOp, xReg, tempReg);   // xReg = xReg op rvalue
            return;
        }

        // Медленный путь: x в стеке или глобале
        _varAccessor.LoadToRegister(name, RegType.r0);           // r0 = старый x
        int slot = _exprGen.SaveRegToStack(RegType.r0);          // сохраняем
        _exprGen.GenerateExpression(rvalue, RegType.r0);         // r0 = rvalue (может затронуть r1..r3)
        _exprGen.RestoreRegFromStack(slot, RegType.r1);          // r1 = старый x
        _exprGen.EmitBinaryOperation(baseOp, RegType.r1, RegType.r0);  // r1 = old op rvalue
        _varAccessor.StoreFromRegister(name, RegType.r1);        // x = r1
    }

    /// <summary>
    /// Универсальная compound-операция для *ptr, obj.field, arr[i].
    /// computeAddress должен записать адрес LValue в переданный регистр (≠ r0).
    /// </summary>
    private void GenerateCompoundIndirect(
        Action<RegType> computeAddress,
        OpCodeSize size,
        string baseOp,
        ASTNode rvalue)
    {
        // 1) адрес → r1
        computeAddress(RegType.r1);
        int addrSlot = _exprGen.SaveRegToStack(RegType.r1);

        // 2) старое значение → r0, сохраняем
        _asm.EmitLoadInd(RegType.r0, RegType.r1, size);
        int valSlot = _exprGen.SaveRegToStack(RegType.r0);

        // 3) rvalue → r0 (может затронуть r1..r3, но они нам сейчас не нужны)
        _exprGen.GenerateExpression(rvalue, RegType.r0);

        // 4) старое значение → r1, применяем op
        _exprGen.RestoreRegFromStack(valSlot, RegType.r1);
        _exprGen.EmitBinaryOperation(baseOp, RegType.r1, RegType.r0);   // r1 = old op rvalue

        // 5) адрес → r2, записываем
        _exprGen.RestoreRegFromStack(addrSlot, RegType.r2);
        _asm.EmitStoreInd(RegType.r1, RegType.r2, size);
    }

    private void GenerateCompoundArrayElement(string arrayName, ASTNode indexExpr, string baseOp, ASTNode rvalue)
    {
        OpCodeSize size = GetArrayElemSize(arrayName);
        GenerateCompoundIndirect(
            reg => _exprGen.ComputeArrayElementAddress(arrayName, indexExpr, reg),
            size, baseOp, rvalue);
    }

    private void GenerateCompoundDeref(DereferenceNode deref, string baseOp, ASTNode rvalue)
    {
        OpCodeSize size = GetPointedSize(deref.Operand);   // уже есть в StatementGenerator
        GenerateCompoundIndirect(
            reg => _exprGen.GenerateExpression(deref.Operand, reg),
            size, baseOp, rvalue);
    }

    private void GenerateCompoundMember(MemberAccessNode member, string baseOp, ASTNode rvalue)
    {
        OpCodeSize size = GetFieldSize(member);            // уже есть в StatementGenerator
        GenerateCompoundIndirect(
            reg => _exprGen.GenerateMemberAddress(member, reg),
            size, baseOp, rvalue);
    }

    private OpCodeSize GetArrayElemSize(string arrayName)
    {
        if (_funcCtx.VarMap.TryGetValue(arrayName, out var loc))
        {
            if (loc.IsArray) return loc.TypeSize;
            if (loc.IsPointer) return CodeGenUtils.GetSizeForType(loc.PointedType!, _asm.ThrowCompiler);
        }
        else if (_globalMem.Contains(arrayName))
        {
            var gInfo = _globalMem.GetInfo(arrayName)!.Value;
            if (gInfo.IsArray) return CodeGenUtils.GetSizeForType(gInfo.Type, _asm.ThrowCompiler);
            if (gInfo.IsPointer) return CodeGenUtils.GetSizeForType(gInfo.PointedType!, _asm.ThrowCompiler);
        }
        _asm.ThrowCompiler.Throw(ErrorCode.CodeGen_NonIndexableType, arrayName);
        return default;
    }

    private OpCodeSize GetFieldSize(MemberAccessNode node)
    {
        var (_, field, _) = ResolveFieldInfo(node);
        return CodeGenUtils.GetSizeForType(field.Type, _asm.ThrowCompiler);
    }

    private (StructLayout layout, FieldInfo field, string fieldType) ResolveFieldInfo(MemberAccessNode node)
    {
        // 1. Собираем цепочку полей от корневого объекта до самого вложенного
        var chain = new List<string>();
        MemberAccessNode? current = node;
        ASTNode? rootObject = null;

        while (current != null)
        {
            chain.Add(current.FieldName);
            if (current.Object is MemberAccessNode inner)
            {
                current = inner;
            }
            else
            {
                rootObject = current.Object;
                break;
            }
        }
        chain.Reverse(); // от внешнего к внутреннему

        // 2. Определяем тип корневого объекта
        string? rootStructType = null;
        switch (rootObject)
        {
            case IdentifierNode id:
                {
                    if (_funcCtx.VarMap.TryGetValue(id.Name, out var loc))
                    {
                        if (loc.StructTypeName != null)
                            rootStructType = loc.StructTypeName;
                        else if (loc.IsPointer && loc.PointedType != null && _structTable.ContainsKey(loc.PointedType))
                            rootStructType = loc.PointedType;
                    }
                    else if (_globalMem.Contains(id.Name))
                    {
                        var gInfo = _globalMem.GetInfo(id.Name)!.Value;
                        if (_structTable.ContainsKey(gInfo.Type))
                            rootStructType = gInfo.Type;
                        else if (gInfo.IsPointer && gInfo.PointedType != null && _structTable.ContainsKey(gInfo.PointedType))
                            rootStructType = gInfo.PointedType;
                    }

                    break;
                }

            case ArrayAccessNode arrAcc:
                rootStructType = _exprGen.ResolveArrayStructType(arrAcc.ArrayName);
                break;
            case BinaryOpNode bin when bin.Operator == "+" || bin.Operator == "-":
                rootStructType = _exprGen.TryInferStructTypeFromPointerExpr(bin);
                if (rootStructType == null)
                    _asm.ThrowCompiler.Throw(ErrorCode.CodeGen_UnknownStructType,
                        "Cannot determine struct type from pointer arithmetic");
                break;
            case DereferenceNode:
                // (*pp) — разыменование указателя на указатель, для потомков
                _asm.ThrowCompiler.Throw(ErrorCode.CodeGen_UnknownStructType,
                    "Dereference member access not implemented yet");
                break;
        }

        if (rootStructType == null)
            _asm.ThrowCompiler.Throw(ErrorCode.CodeGen_UnknownStructType, "Cannot determine struct type for member access");

        // 3. Проходим по цепочке, получая раскладку и поле на каждом уровне
        StructLayout? currentLayout = null;
        FieldInfo? currentField = null;
        string currentType = rootStructType;

        foreach (var fieldName in chain)
        {
            if (!_structTable.TryGetValue(currentType, out var layout))
                _asm.ThrowCompiler.Throw(ErrorCode.CodeGen_UnknownStructType, currentType);
            currentLayout = layout;
            currentField = layout.GetField(fieldName)
                           ?? _asm.ThrowCompiler.Throw<FieldInfo>(ErrorCode.CodeGen_UnknownField, fieldName, currentType);
            currentType = currentField.Type;
        }

        return (currentLayout!, currentField!, currentType);
    }

    private OpCodeSize GetPointedSize(ASTNode expr)
    {
        if (expr is IdentifierNode id)
        {
            if (_funcCtx.VarMap.TryGetValue(id.Name, out var loc) && loc.IsPointer)
                return CodeGenUtils.GetSizeForType(loc.PointedType!, _asm.ThrowCompiler);
            if (_globalMem.Contains(id.Name))
            {
                var gInfo = _globalMem.GetInfo(id.Name)!.Value;
                if (gInfo.IsPointer)
                    return CodeGenUtils.GetSizeForType(gInfo.PointedType!, _asm.ThrowCompiler);
            }
        }
        return _asm.ThrowCompiler.Throw<OpCodeSize>(ErrorCode.CodeGen_UnknownPointedType);
    }
    private void GenerateIf(IfNode ifNode)
    {
        string elseLabel = _getLabel();
        string endLabel = ifNode.ElseBlock != null ? _getLabel() : elseLabel;

        _exprGen.GenerateCondition(ifNode.Condition, null, elseLabel);
        GenerateBlock(ifNode.ThenBlock);

        if (ifNode.ElseBlock != null)
        {
            _asm.EmitJmp(endLabel);
            _asm.MarkLabel(elseLabel);
            GenerateBlock(ifNode.ElseBlock);
            _asm.MarkLabel(endLabel);
        }
        else
        {
            _asm.MarkLabel(elseLabel);
        }
    }

    private void GenerateWhile(WhileNode whileNode)
    {
        string startLabel = _getLabel();
        string endLabel = _getLabel();
        _loopStack.Push((endLabel, startLabel));   // continue → startLabel

        _asm.MarkLabel(startLabel);
        _exprGen.GenerateCondition(whileNode.Condition, null, endLabel);
        GenerateBlock(whileNode.Body);
        _asm.EmitJmp(startLabel);
        _asm.MarkLabel(endLabel);

        _loopStack.Pop();
    }
    private void GenerateSingleInit(ASTNode init)
    {
        if (init is VariableNode varInit)
        {
            if (varInit.Initializer != null)
            {
                if (_funcCtx.VarMap.TryGetValue(varInit.Name, out var varLoc) && varLoc.IsRegister)
                    _exprGen.GenerateExpression(varInit.Initializer, varLoc.Register);
                else
                {
                    _exprGen.GenerateExpression(varInit.Initializer);
                    _varAccessor.StoreFromRegister(varInit.Name, RegType.r0);
                }
            }
        }
        else if (init is AssignmentNode assignInit)
            GenerateAssignment(assignInit);
        else if (init is BlockNode block)
            foreach (var stmt in block.Statements) GenerateSingleInit(stmt);
        else
            _exprGen.GenerateExpression(init);
    }

    private void GenerateFor(ForNode forNode)
    {
        if (forNode.Init != null) GenerateSingleInit(forNode.Init);

        string startLabel = _getLabel();
        string continueLabel = _getLabel();
        string endLabel = _getLabel();

        _asm.MarkLabel(startLabel);
        if (forNode.Condition != null)
            _exprGen.GenerateCondition(forNode.Condition, null, endLabel);

        _loopStack.Push((endLabel, continueLabel));

        GenerateBlock(forNode.Body);

        _asm.MarkLabel(continueLabel);              // continue → increment
        if (forNode.Increment != null)
        {
            if (forNode.Increment is AssignmentNode assignIncr)
                GenerateAssignment(assignIncr);
            else
                _exprGen.GenerateExpression(forNode.Increment);
        }
        _asm.EmitJmp(startLabel);
        _asm.MarkLabel(endLabel);

        _loopStack.Pop();
    }

    private void GenerateDoWhile(DoWhileNode node)
    {
        string startLabel = _getLabel();
        string continueLabel = _getLabel();
        string endLabel = _getLabel();

        _asm.MarkLabel(startLabel);
        _loopStack.Push((endLabel, continueLabel));
        GenerateBlock((BlockNode)node.Body);
        _loopStack.Pop();

        _asm.MarkLabel(continueLabel);
        _exprGen.GenerateCondition(node.Condition, startLabel, endLabel);
        _asm.MarkLabel(endLabel);
    }
    private void GenerateReturn(ReturnNode ret)
    {
        if (ret.Value != null)
            _exprGen.GenerateExpression(ret.Value);
        if (_funcCtx.FunctionName == "main")
            _asm.EmitEnd();
        else
            _asm.EmitJmp(_funcCtx.EpilogueLabel);
    }

    private void GenerateBreak()
    {
        if (_loopStack.Count == 0)
            _asm.ThrowCompiler.Throw(ErrorCode.CodeGen_BreakOutsideLoop);
        _asm.EmitJmp(_loopStack.Peek().BreakLabel);
    }

    private void GenerateContinue()
    {
        if (_loopStack.Count == 0)
            _asm.ThrowCompiler.Throw(ErrorCode.CodeGen_ContinueOutsideLoop);
        _asm.EmitJmp(_loopStack.Peek().ContinueLabel);
    }

}
