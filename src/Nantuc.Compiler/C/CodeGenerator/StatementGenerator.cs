using Nantuc.Common;
using Nantuc.Compiler.ASM;
using Nantuc.Compiler.ASM.Base;
using Nantuc.Compiler.ASM.IR;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C.AST;
using Nantuc.Diagnostics;

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
                            _exprGen.GenerateExpression(var.Initializer, varLoc.Register);
                            _asm.EmitCastToType(varLoc.Register, varLoc.TypeSize, varLoc.IsSigned);   // NEW
                        }
                        else
                        {
                            _exprGen.GenerateExpression(var.Initializer);
                            _varAccessor.StoreFromRegister(var.Name, RegType.r0);   // truncates/casts
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
            _asm.ErrorReporter.Throw(ErrorCode.CodeGen_CannotGetRegisterAddress, var.Name);

        OpCodeSize elemSize = loc.TypeSize;
        int elemBytes = CodeGenUtils.GetSizeInBytes(elemSize, _asm.ErrorReporter);
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
        _exprGen.CheckConstTarget(assign);
        
        if (assign.CompoundOp != null && assign.CompoundOp != "=")
        {
            GenerateCompoundAssignment(assign);
            return;
        }
        // 1) arr[i] = value
        if (assign.IndexExpr != null)
        {
            _exprGen.GenerateExpression(assign.Value);       // value -> r1
            _exprGen.StoreR0ToArrayElement(assign.Name, assign.IndexExpr);  // внутри save
            return;
        }

        // 2) *ptr = value
        if (assign.LValue is DereferenceNode deref)
        {
            _exprGen.GenerateExpression(assign.Value);       // value -> r1
            int slot = _exprGen.SaveRegToStack(RegType.r0);

            _exprGen.GenerateExpression(deref.Operand, RegType.r0);   // ptr -> r1
            OpCodeSize size = GetPointedSize(deref.Operand);

            _exprGen.RestoreRegFromStack(slot, RegType.r1);           // value -> r2
            _asm.EmitStoreInd(RegType.r1, RegType.r0, size);
            return;
        }

        // 3) obj.field = value (включая a[i].field = value, p->field = value)
        if (assign.LValue is MemberAccessNode memberAccess)
        {
            _exprGen.GenerateExpression(assign.Value);       // value -> r1
            int slot = _exprGen.SaveRegToStack(RegType.r0);

            _exprGen.GenerateMemberAddress(memberAccess, RegType.r0);  // &field -> r1
            OpCodeSize size = GetFieldSize(memberAccess);

            _exprGen.RestoreRegFromStack(slot, RegType.r1);            // value -> r2
            _asm.EmitStoreInd(RegType.r1, RegType.r0, size);
            return;
        }

        // 4) x = <простое значение>  — оптимизация без save
        if (assign.IndexExpr == null && assign.LValue == null &&
    _funcCtx.VarMap.TryGetValue(assign.Name, out var regLoc) && regLoc.IsRegister &&
    (assign.Value is NumberNode || assign.Value is IdentifierNode))
        {
            _exprGen.GenerateExpression(assign.Value, regLoc.Register);
            _asm.EmitCastToType(regLoc.Register, regLoc.TypeSize, regLoc.IsSigned);   // NEW
            return;
        }

        // 5) x = x op expr  — оптимизация без save
        if (_funcCtx.VarMap.TryGetValue(assign.Name, out var loc) && loc.IsRegister)
        {
            if (assign.Value is BinaryOpNode binop && binop.Left is IdentifierNode id && id.Name == assign.Name
                && (binop.Operator is "+" or "-" or "*" or "/"))
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
            }
            else
            {
                _exprGen.GenerateExpression(assign.Value, loc.Register);
            }
            _asm.EmitCastToType(loc.Register, loc.TypeSize, loc.IsSigned);   // NEW (единая точка)
            return;
        }

        // 6) Общий случай: x (в стеке/глобале) = value
        _exprGen.GenerateExpression(assign.Value);
        _varAccessor.StoreFromRegister(assign.Name, RegType.r0);
    }

    private void GenerateCompoundAssignment(AssignmentNode assign)
    {
        // CompoundOp гарантированно не null и не "=" (проверено в GenerateAssignment)
        string baseOp = assign.CompoundOp!.TrimEnd('=');   // "+=" -> "+", "<<=" -> "<<"

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
        if(_varAccessor.TryGetRegister(name, out RegType xReg))
        {
            RegType tempReg = (xReg != RegType.r0) ? RegType.r0 : RegType.r1;
            _exprGen.GenerateExpression(rvalue, tempReg);
            _exprGen.EmitBinaryOperation(baseOp, xReg, tempReg);
            if (_funcCtx.VarMap.TryGetValue(name, out var loc))
                _asm.EmitCastToType(xReg, loc.TypeSize, loc.IsSigned);
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

    private (OpCodeSize size, bool isSigned) GetArrayElemInfo(string name)  // переделать GetArrayElemSize
    {
        if (_funcCtx.VarMap.TryGetValue(name, out var loc))
        {
            if (loc.IsArray) return (loc.TypeSize, loc.IsSigned);
            if (loc.IsPointer && loc.PointedType != null)
                return (CodeGenUtils.GetSizeForType(loc.PointedType, _asm.ErrorReporter),
                        CodeGenUtils.IsSignedType(loc.PointedType));
        }
        else if (_globalMem.Contains(name))
        {
            var g = _globalMem.GetInfo(name)!.Value;
            if (g.IsArray)
            {
                var sz = g.IsPointer ? OpCodeSize.S64
                                     : CodeGenUtils.GetSizeForType(g.Type, _asm.ErrorReporter);
                return (sz, !g.IsPointer && CodeGenUtils.IsSignedType(g.Type));
            }
            if (g.IsPointer && g.PointedType != null)
                return (CodeGenUtils.GetSizeForType(g.PointedType, _asm.ErrorReporter),
                        CodeGenUtils.IsSignedType(g.PointedType));
        }
        _asm.ErrorReporter.Throw(ErrorCode.CodeGen_NonIndexableType, name);
        return default;
    }

    /// <summary>
    /// Универсальная compound-операция для *ptr, obj.field, arr[i].
    /// computeAddress должен записать адрес LValue в переданный регистр (≠ r0).
    /// </summary>
    private void GenerateCompoundIndirect(
        Action<RegType> computeAddress, OpCodeSize size, bool isSigned,
        string baseOp, ASTNode rvalue)
    {
        computeAddress(RegType.r1);
        int addrSlot = _exprGen.SaveRegToStack(RegType.r1);

        _asm.EmitLoadInd(RegType.r0, RegType.r1, size);
        _asm.EmitCastToType(RegType.r0, size, isSigned);                 // NEW — load
        int valSlot = _exprGen.SaveRegToStack(RegType.r0);

        _exprGen.GenerateExpression(rvalue, RegType.r0);

        _exprGen.RestoreRegFromStack(valSlot, RegType.r1);
        _exprGen.EmitBinaryOperation(baseOp, RegType.r1, RegType.r0);

        _asm.EmitCastToType(RegType.r1, size, isSigned);                 // NEW — result

        _exprGen.RestoreRegFromStack(addrSlot, RegType.r2);
        _asm.EmitStoreInd(RegType.r1, RegType.r2, size);
    }

    // ============================================================
    // Signedness спутники к GetArrayElemSize / GetPointedSize / GetFieldSize.
    // Возвращают false, если определить не удалось (безопасный default —
    // CASTZ маскирует по размеру, что для unsigned-типов и есть канон).
    // ============================================================

    private bool IsArrayElemSigned(string arrayName)
    {
        if (_funcCtx.VarMap.TryGetValue(arrayName, out var loc))
        {
            if (loc.IsArray)
                return loc.IsSigned;
            if (loc.IsPointer && loc.PointedType != null)
                return CodeGenUtils.IsSignedType(loc.PointedType);
        }
        else if (_globalMem.Contains(arrayName))
        {
            var gInfo = _globalMem.GetInfo(arrayName)!.Value;
            if (gInfo.IsArray)
                return !gInfo.IsPointer && CodeGenUtils.IsSignedType(gInfo.Type);
            if (gInfo.IsPointer && gInfo.PointedType != null)
                return CodeGenUtils.IsSignedType(gInfo.PointedType);
        }
        return false;
    }

    /// <summary>
    /// Signedness pointed-типа для <c>*operand</c>. Зеркалит <see cref="GetPointedSize"/>;
    /// вызывать после успешного <see cref="GetPointedSize"/> — тогда определение гарантировано.
    /// </summary>
    private bool IsPointedSigned(ASTNode operand)
    {
        if (operand is IdentifierNode id)
        {
            if (_funcCtx.VarMap.TryGetValue(id.Name, out var loc) && loc.IsPointer)
                return loc.PointedType != null && CodeGenUtils.IsSignedType(loc.PointedType);
            if (_globalMem.Contains(id.Name))
            {
                var gInfo = _globalMem.GetInfo(id.Name)!.Value;
                if (gInfo.IsPointer)
                    return gInfo.PointedType != null && CodeGenUtils.IsSignedType(gInfo.PointedType);
            }
        }
        return false;   // не-Identifier deref (сложное выражение) — считаем unsigned
    }

    /// <summary>
    /// Signedness поля структуры. Зеркалит <see cref="GetFieldSize"/>;
    /// использует тот же <see cref="ResolveFieldInfo"/>, так что цепочки <c>a.b.c</c> и <c>p->f</c> разрешаются одинаково.
    /// </summary>
    private bool IsFieldSigned(MemberAccessNode node)
    {
        var (_, field, _) = ResolveFieldInfo(node);
        return CodeGenUtils.IsSignedType(field.Type);
    }
    private void GenerateCompoundArrayElement(string arrayName, ASTNode indexExpr, string baseOp, ASTNode rvalue)
    {
        OpCodeSize size = GetArrayElemSize(arrayName);
        bool isSigned = IsArrayElemSigned(arrayName);
        GenerateCompoundIndirect(
            reg => _exprGen.ComputeArrayElementAddress(arrayName, indexExpr, reg),
            size, isSigned, baseOp, rvalue);
    }

    private void GenerateCompoundDeref(DereferenceNode deref, string baseOp, ASTNode rvalue)
    {
        OpCodeSize size = GetPointedSize(deref.Operand);
        bool isSigned = IsPointedSigned(deref.Operand);
        GenerateCompoundIndirect(
            reg => _exprGen.GenerateExpression(deref.Operand, reg),
            size, isSigned, baseOp, rvalue);
    }

    private void GenerateCompoundMember(MemberAccessNode member, string baseOp, ASTNode rvalue)
    {
        OpCodeSize size = GetFieldSize(member);
        bool isSigned = IsFieldSigned(member);
        GenerateCompoundIndirect(
            reg => _exprGen.GenerateMemberAddress(member, reg),
            size, isSigned, baseOp, rvalue);
    }

    private OpCodeSize GetArrayElemSize(string arrayName)
    {
        if (_funcCtx.VarMap.TryGetValue(arrayName, out var loc))
        {
            if (loc.IsArray) return loc.TypeSize;
            if (loc.IsPointer) return CodeGenUtils.GetSizeForType(loc.PointedType!, _asm.ErrorReporter);
        }
        else if (_globalMem.Contains(arrayName))
        {
            var gInfo = _globalMem.GetInfo(arrayName)!.Value;
            if (gInfo.IsArray) return gInfo.IsPointer
                                        ? OpCodeSize.S64
                                        : CodeGenUtils.GetSizeForType(gInfo.Type, _asm.ErrorReporter);
            if (gInfo.IsPointer) return CodeGenUtils.GetSizeForType(gInfo.PointedType!, _asm.ErrorReporter);
        }
        _asm.ErrorReporter.Throw(ErrorCode.CodeGen_NonIndexableType, arrayName);
        return default;
    }

    private OpCodeSize GetFieldSize(MemberAccessNode node)
    {
        var (_, field, _) = ResolveFieldInfo(node);
        return CodeGenUtils.GetSizeForType(field.Type, _asm.ErrorReporter);
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
                    _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UnknownStructType,
                        "Cannot determine struct type from pointer arithmetic");
                break;
            case DereferenceNode:
                // (*pp) — разыменование указателя на указатель, для потомков
                _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UnknownStructType,
                    "Dereference member access not implemented yet");
                break;
        }

        if (rootStructType == null)
            _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UnknownStructType, "Cannot determine struct type for member access");

        // 3. Проходим по цепочке, получая раскладку и поле на каждом уровне
        StructLayout? currentLayout = null;
        FieldInfo? currentField = null;
        string currentType = rootStructType;

        foreach (var fieldName in chain)
        {
            if (!_structTable.TryGetValue(currentType, out var layout))
                _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UnknownStructType, currentType);
            currentLayout = layout;
            currentField = layout.GetField(fieldName)
                           ?? _asm.ErrorReporter.Throw<FieldInfo>(ErrorCode.CodeGen_UnknownField, fieldName, currentType);
            currentType = currentField.Type;
        }

        return (currentLayout!, currentField!, currentType);
    }

    private OpCodeSize GetPointedSize(ASTNode expr)
    {
        if (expr is IdentifierNode id)
        {
            if (_funcCtx.VarMap.TryGetValue(id.Name, out var loc) && loc.IsPointer)
                return CodeGenUtils.GetSizeForType(loc.PointedType!, _asm.ErrorReporter);
            if (_globalMem.Contains(id.Name))
            {
                var gInfo = _globalMem.GetInfo(id.Name)!.Value;
                if (gInfo.IsPointer)
                    return CodeGenUtils.GetSizeForType(gInfo.PointedType!, _asm.ErrorReporter);
            }
        }
        return _asm.ErrorReporter.Throw<OpCodeSize>(ErrorCode.CodeGen_UnknownPointedType);
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
        _loopStack.Push((endLabel, startLabel));   // continue -> startLabel

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
                {
                    _exprGen.GenerateExpression(varInit.Initializer, varLoc.Register);
                    _asm.EmitCastToType(varLoc.Register, varLoc.TypeSize, varLoc.IsSigned);
                }
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

        _asm.MarkLabel(continueLabel);              // continue -> increment
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
        {
            _exprGen.GenerateExpression(ret.Value);
            if (!_funcCtx.ReturnIsVoid && _funcCtx.ReturnSize != OpCodeSize.S64)
                _asm.EmitCastToType(RegType.r0, _funcCtx.ReturnSize, _funcCtx.ReturnIsSigned);
        }
        if (_funcCtx.FunctionName == "main") _asm.EmitEnd();
        else _asm.EmitJmp(_funcCtx.EpilogueLabel);
    }
    private void GenerateBreak()
    {
        if (_loopStack.Count == 0)
            _asm.ErrorReporter.Throw(ErrorCode.CodeGen_BreakOutsideLoop);
        _asm.EmitJmp(_loopStack.Peek().BreakLabel);
    }

    private void GenerateContinue()
    {
        if (_loopStack.Count == 0)
            _asm.ErrorReporter.Throw(ErrorCode.CodeGen_ContinueOutsideLoop);
        _asm.EmitJmp(_loopStack.Peek().ContinueLabel);
    }

}
