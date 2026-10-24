using Nantuc.Compiler.ASM.Base;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Diagnostics;

namespace Nantuc.Compiler.C.CodeGenerator;

// ============================================================
// ExpressionGenerator – генерация выражений
// ============================================================
public class ExpressionGenerator(AssemblerBase asm,
                                 GlobalMemoryManager globalMem,
                                 FunctionContext funcCtx,
                                 Dictionary<string, FunctionNode> functionTable,
                                 Func<string> getLabel,
                                 Dictionary<string, StructLayout> structTable,
                                 VariableAccessor varAccessor)
{
    private readonly AssemblerBase _asm = asm;
    private readonly GlobalMemoryManager _globalMem = globalMem;
    private readonly FunctionContext _funcCtx = funcCtx;
    private readonly Dictionary<string, FunctionNode> _functionTable = functionTable;
    private readonly Dictionary<string, StructLayout> _structTable = structTable;
    private readonly Func<string> _getLabel = getLabel;
    private readonly VariableAccessor _varAccessor = varAccessor;

    public void GenerateExpression(ASTNode expr)
    {
        switch (expr)
        {
            case NumberNode num:
                _asm.EmitLdi(RegType.r0, (ulong)num.Value);
                break;
            case UnaryOpNode unop:
                GenerateUnaryOp(unop, RegType.r0);   // было GenerateUnaryOp(unop)
                break;
            case IdentifierNode id:
                if (IsArrayIdentifier(id.Name))
                    _varAccessor.LoadAddressToRegister(id.Name, RegType.r0);
                else
                    _varAccessor.LoadToRegister(id.Name, RegType.r0);
                break;
            case BinaryOpNode binop:
                GenerateBinaryOp(binop, RegType.r0);
                break;
            case FunctionCallNode call:
                GenerateFunctionCall(call);
                break;
            case ArrayAccessNode arrAcc:
                LoadArrayElementToR0(arrAcc.ArrayName, arrAcc.Index, RegType.r0);
                break;
            case AddressOfNode addrOf:
                GenerateAddressOf(addrOf, RegType.r0);
                break;
            case MemberAccessNode member:
                GenerateMemberAccess(member, RegType.r0);
                break;
            case DereferenceNode deref:
                GenerateDereference(deref, RegType.r0);
                break;

            default:
                ThrowHelper.ThrowMiniC(ErrorCode.NotSupported, $"Unsupported expression: {expr.GetType()}");
                break;
        }
    }

    public void GenerateExpression(ASTNode expr, RegType targetReg)
    {
        if (targetReg == RegType.r0)
        {
            GenerateExpression(expr); // обычный путь
            return;
        }

        // Генерируем в другой регистр
        switch (expr)
        {
            case NumberNode num:
                _asm.EmitLdi(targetReg, (ulong)num.Value);
                break;
            case IdentifierNode id:
                if (IsArrayIdentifier(id.Name))
                    _varAccessor.LoadAddressToRegister(id.Name, targetReg);
                else
                    _varAccessor.LoadToRegister(id.Name, targetReg);
                break;
            case BinaryOpNode binop:
                GenerateBinaryOp(binop, targetReg);
                break;
            case UnaryOpNode unop:
                GenerateUnaryOp(unop, targetReg);
                break;
            case DereferenceNode deref:
                GenerateDereference(deref, targetReg);
                break;
            case AddressOfNode addrOf:
                GenerateAddressOf(addrOf, targetReg);
                break;
            case ArrayAccessNode arrAcc:
                LoadArrayElementToR0(arrAcc.ArrayName, arrAcc.Index, targetReg);
                break;
            case MemberAccessNode member:
                GenerateMemberAccess(member, targetReg);
                break;
            default:
                GenerateExpression(expr);
                if (targetReg != RegType.r0)
                    _asm.EmitMov(targetReg, RegType.r0);
                break;
        }
    }

    private bool IsArrayIdentifier(string name)
    {
        if (_funcCtx.VarMap.TryGetValue(name, out var loc))
            return loc.IsArray;
        if (_globalMem.Contains(name))
            return _globalMem.GetInfo(name)!.Value.IsArray;
        return false;
    }

    internal int SaveRegToStack(RegType reg)
    {
        int slot = _funcCtx.TotalLocalSize + _funcCtx.ExpressionSaveDepth * 8;
        _funcCtx.ExpressionSaveDepth++;

        _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)slot);
        _asm.EmitAdd(CodeGenUtils.ADDR_SCRATCH_REG_EN, RegType.rSP);
        _asm.EmitStoreInd(reg, CodeGenUtils.ADDR_SCRATCH_REG_EN, OpCodeSize.S64);
        return slot;
    }

    internal void RestoreRegFromStack(int slot, RegType reg)
    {
        _funcCtx.ExpressionSaveDepth--;

        _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)slot);
        _asm.EmitAdd(CodeGenUtils.ADDR_SCRATCH_REG_EN, RegType.rSP);
        _asm.EmitLoadInd(reg, CodeGenUtils.ADDR_SCRATCH_REG_EN, OpCodeSize.S64);
    }

    internal void ReleaseSaveSlot() => _funcCtx.ExpressionSaveDepth--;

    private void GenerateBinaryOp(BinaryOpNode binop, RegType targetReg)
    {
        if (CodeGenUtils.IsComparisonOperator(binop.Operator))
        {
            // Вычисляем left и right с сохранением, Cmp, затем превращаем в 1/0
            GenerateExpression(binop.Left, RegType.r0);
            int slot = SaveRegToStack(RegType.r0);
            GenerateExpression(binop.Right, RegType.r0);
            RestoreRegFromStack(slot, RegType.r1);   // left в r1
            _asm.EmitCmp(RegType.r1, RegType.r0);    // flags = left - right

            string trueLabel = _getLabel();
            string endLabel = _getLabel();

            _asm.EmitLdi(targetReg, 0);              // результат = 0 по умолчанию
            GenerateComparisonJump(binop.Operator, trueLabel, endLabel);
            // Если false — прыжок на endLabel уже сделан, targetReg == 0
            _asm.MarkLabel(trueLabel);
            _asm.EmitLdi(targetReg, 1);
            _asm.MarkLabel(endLabel);
            return;
        }
        if (binop.Operator == ",")
        {
            GenerateExpression(binop.Left);
            GenerateExpression(binop.Right, targetReg);
            return;
        }
        // обработка логических &&, || без изменений
        if (binop.Operator == "&&" || binop.Operator == "||")
        {
            GenerateLogicalAndOr(binop); // результат всегда в r0
            if (targetReg != RegType.r0)
                _asm.EmitMov(targetReg, RegType.r0);
            return;
        }

        // сворачивание констант
        if (binop.Left is NumberNode l && binop.Right is NumberNode r)
        {
            long result = ComputeConstant(l.Value, r.Value, binop.Operator);
            _asm.EmitLdi(targetReg, (ulong)result);
            return;
        }

        // Попытка оптимизации: targetReg совпадает с регистром левого операнда
        if (binop.Left is IdentifierNode leftId && _varAccessor.TryGetRegister(leftId.Name, out var leftReg) && leftReg == targetReg)
        {
            // генерируем правый операнд во временный регистр (например, r1, если targetReg != r1, иначе r2)
            RegType tempReg = (targetReg != RegType.r1) ? RegType.r1 : RegType.r2;
            GenerateExpression(binop.Right, tempReg);
            EmitBinaryOperation(binop.Operator, targetReg, tempReg);
            return;
        }

        // Попытка оптимизации: targetReg совпадает с регистром правого операнда (для коммутативных операций)
        if (binop.Right is IdentifierNode rightId && _varAccessor.TryGetRegister(rightId.Name, out var rightReg) && rightReg == targetReg
            && (binop.Operator == "+" || binop.Operator == "*")) // только коммутативные
        {
            RegType tempReg = (targetReg != RegType.r0) ? RegType.r0 : RegType.r1;
            GenerateExpression(binop.Left, tempReg);
            EmitBinaryOperation(binop.Operator, targetReg, tempReg);
            return;
        }

        // Общий случай
        GenerateExpression(binop.Left, targetReg);
        RegType rReg = (targetReg != RegType.r1) ? RegType.r1 : RegType.r2;
        RegType addrReg = RegType.r3;

        int saveOffset = _funcCtx.TotalLocalSize + _funcCtx.ExpressionSaveDepth * 8;

        // Сохраняем левый операнд в save slot [rSP + saveOffset]
        _asm.EmitLdi(addrReg, (ulong)saveOffset);
        _asm.EmitAdd(addrReg, RegType.rSP);
        _asm.EmitStoreInd(targetReg, addrReg, OpCodeSize.S64);

        _funcCtx.ExpressionSaveDepth++;

        GenerateExpression(binop.Right, rReg);

        _funcCtx.ExpressionSaveDepth--;

        // Восстанавливаем левый операнд
        _asm.EmitLdi(addrReg, (ulong)saveOffset);
        _asm.EmitAdd(addrReg, RegType.rSP);
        _asm.EmitLoadInd(targetReg, addrReg, OpCodeSize.S64);

        EmitBinaryOperation(binop.Operator, targetReg, rReg);
    }
    private void GenerateUnaryOp(UnaryOpNode unop, RegType targetReg)
    {
        switch (unop.Operator)
        {
            case "-":
                GenerateExpression(unop.Operand, targetReg);
                _asm.EmitNot(targetReg);
                _asm.EmitInc(targetReg);
                break;

            case "!":
                {
                    GenerateExpression(unop.Operand, targetReg);           // targetReg = operand
                    RegType scratch = targetReg == RegType.r1 ? RegType.r2 : RegType.r1;
                    _asm.EmitLdi(scratch, 0);
                    _asm.EmitCmp(targetReg, scratch);   // flags = targetReg - 0

                    string endLabel = _getLabel();
                    _asm.EmitLdi(targetReg, 0);
                    _asm.EmitJnz(endLabel);
                    _asm.EmitLdi(targetReg, 1);
                    _asm.MarkLabel(endLabel);
                    break;
                }

            case "~":
                GenerateExpression(unop.Operand, targetReg);
                _asm.EmitNot(targetReg);
                break;

            default:
                ThrowHelper.ThrowMiniC(ErrorCode.NotSupported,
                    $"Unsupported unary operator: {unop.Operator}");
                return;
        }
    }
    public void GenerateMemberAddress(MemberAccessNode node, RegType targetReg)
    {
        string? structType = null;
        switch (node.Object)
        {
            case IdentifierNode identifer:
                _varAccessor.LoadAddressToRegister(identifer.Name, targetReg);
                return;
            case MemberAccessNode node1:
                {
                    // Рекурсивно вычисляем адрес вложенного объекта (результат в targetReg)
                    GenerateMemberAddress(node1, targetReg);

                    // Получаем ТИП поля node1, чтобы понять, в какой структуре искать текущее поле
                    var (_, _, innerFieldType) = ResolveMemberAccessType(node1);

                    if (!_structTable.TryGetValue(innerFieldType, out var innerLayout))
                        ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UnknownStructType, $"Unknown struct type '{innerFieldType}'", innerFieldType);

                    var currentField = innerLayout.GetField(node.FieldName)
                                       ?? ThrowHelper.ThrowMiniC<FieldInfo>(ErrorCode.CodeGen_UnknownField, node.FieldName, innerFieldType);

                    if (currentField.Offset > 0)
                    {
                        _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)currentField.Offset);
                        _asm.EmitAdd(targetReg, CodeGenUtils.ADDR_SCRATCH_REG_EN);
                    }
                    return;
                }

            case DereferenceNode:
                ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UnknownStructType, "Dereference member access not implemented yet");
                return;
            case ArrayAccessNode arrAcc:
                {
                    string? arrStructType = ResolveArrayStructType(arrAcc.ArrayName);
                    var layoutstr = _structTable[arrStructType!];
                    var fieldstr = layoutstr.GetField(node.FieldName)
                                   ?? ThrowHelper.ThrowMiniC<FieldInfo>(ErrorCode.CodeGen_UnknownField, node.FieldName, arrStructType!);

                    GenerateExpression(arrAcc.Index, targetReg);   // index → targetReg
                    _asm.EmitMov(RegType.r3, targetReg);           // save index в r4

                    _varAccessor.LoadAddressToRegister(arrAcc.ArrayName, targetReg);  // base → targetReg

                    int elemSize = layoutstr.Size;
                    if (elemSize > 1)
                        _asm.EmitMultiplyByConstant(RegType.r3, elemSize);

                    _asm.EmitAdd(targetReg, RegType.r3);           // base + offset

                    if (fieldstr.Offset > 0)
                    {
                        _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)fieldstr.Offset);
                        _asm.EmitAdd(targetReg, CodeGenUtils.ADDR_SCRATCH_REG_EN);
                    }
                    return;
                }

            default:
                ThrowHelper.ThrowMiniC(ErrorCode.NotSupported, "Unsupported object for member access");
                break;
        }

        if (structType == null)
            ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UnknownStructType, "Cannot determine struct type for member access");

        var layout = _structTable[structType];
        var field = layout.GetField(node.FieldName) ?? ThrowHelper.ThrowMiniC<FieldInfo>(ErrorCode.CodeGen_UnknownField, node.FieldName, structType);

        if (node.IsArrow)
        {
            GenerateExpression(node.Object, targetReg);  // r0 = ptr
        }
        else
        {
            if (node.Object is IdentifierNode identifer)
                _varAccessor.LoadToRegister(identifer.Name, targetReg);
            else ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_InvalidDotAccess, "Dot address only for simple variables");
        }
        if (field.Offset > 0)
        {
            _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)field.Offset);
            _asm.EmitAdd(targetReg, CodeGenUtils.ADDR_SCRATCH_REG_EN);
        }
    }


    private void GenerateMemberAccess(MemberAccessNode node, RegType targetReg)
    {
        // 1. Определить StructLayout
        // Нужно узнать тип объекта. Он может быть переменной (IdentifierNode) или другим выражением.
        string? structType = null;
        switch (node.Object)
        {
            case IdentifierNode id:
                {
                    // Ищем в локальных или глобальных
                    if (_funcCtx.VarMap.TryGetValue(id.Name, out var loc))
                    {
                        if (loc.StructTypeName != null)
                            structType = loc.StructTypeName;
                        else if (loc.IsPointer && loc.PointedType != null && _structTable.ContainsKey(loc.PointedType))
                            structType = loc.PointedType; // ptr->field
                        else ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_NonStructAccess, id.Name);
                    }
                    else if (_globalMem.Contains(id.Name))
                    {
                        var gInfo = _globalMem.GetInfo(id.Name)!.Value;
                        if (_structTable.ContainsKey(gInfo.Type))
                            structType = gInfo.Type;
                        else if (gInfo.IsPointer && gInfo.PointedType != null && _structTable.ContainsKey(gInfo.PointedType))
                            structType = gInfo.PointedType;
                        else ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_NonStructAccess, id.Name);
                    }
                    else
                        ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UndefinedVariable, id.Name);
                    break;
                }

            case MemberAccessNode:
                {
                    // Рекурсивно вычисляем адрес вложенного объекта (без загрузки значения) в r0
                    GenerateMemberAddress(node, targetReg); // этот метод вычислит адрес поля
                                                            // Теперь r0 содержит адрес поля, загружаем значение
                    var (_, fieldMem, _) = ResolveMemberAccessType(node);
                    OpCodeSize fieldSizeMem = CodeGenUtils.GetSizeForType(fieldMem.Type);
                    _asm.EmitLoadInd(targetReg, targetReg, fieldSizeMem);
                    return;
                }

            case DereferenceNode:
                ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UnknownStructType, "Dereference member access not implemented yet");
                return;
            case ArrayAccessNode arrAcc:
                {
                    string? arrStructType = ResolveArrayStructType(arrAcc.ArrayName);
                    var layoutstr = _structTable[arrStructType!];
                    var fieldstr = layoutstr.GetField(node.FieldName)
                                ?? ThrowHelper.ThrowMiniC<FieldInfo>(ErrorCode.CodeGen_UnknownField, node.FieldName, arrStructType!);

                    // 1) index → targetReg
                    GenerateExpression(arrAcc.Index, targetReg);

                    // 2) save index в r4 (r4 не используется как targetReg, безопасен)
                    _asm.EmitMov(RegType.r3, targetReg);

                    // 3) base address → targetReg (НЕ LoadToRegister!)
                    _varAccessor.LoadAddressToRegister(arrAcc.ArrayName, targetReg);

                    // 4) index * elemSize (в r4)
                    int elemSize = layoutstr.Size;
                    if (elemSize > 1)
                        _asm.EmitMultiplyByConstant(RegType.r3, elemSize);

                    // 5) targetReg = base + offset
                    _asm.EmitAdd(targetReg, RegType.r3);

                    // 6) + field offset
                    if (fieldstr.Offset > 0)
                    {
                        _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)fieldstr.Offset);
                        _asm.EmitAdd(targetReg, CodeGenUtils.ADDR_SCRATCH_REG_EN);
                    }

                    // 7) value → targetReg
                    OpCodeSize fieldSizestr = CodeGenUtils.GetSizeForType(fieldstr.Type);
                    _asm.EmitLoadInd(targetReg, targetReg, fieldSizestr);
                    return;
                }
            default:
                ThrowHelper.ThrowMiniC(ErrorCode.NotSupported, "Unsupported object for member access");
                break;
        }

        if (structType == null)
            ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UnknownStructType, "Cannot determine struct type for member access");

        var layout = _structTable[structType];
        var field = layout.GetField(node.FieldName) ?? ThrowHelper.ThrowMiniC<FieldInfo>(ErrorCode.CodeGen_UnknownField, node.FieldName, structType);

        if (node.IsArrow)
        {
            GenerateExpression(node.Object, targetReg);
        }
        else
        {
            if (node.Object is IdentifierNode identifer)
            {
                _varAccessor.LoadAddressToRegister(identifer.Name, targetReg);
            }
            else
            {
                ThrowHelper.ThrowMiniC(ErrorCode.NotSupported, "Dot access only supported for simple variables currently");
            }
        }

        if (field.Offset > 0)
        {
            _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)field.Offset);
            _asm.EmitAdd(targetReg, CodeGenUtils.ADDR_SCRATCH_REG_EN);
        }

        // 4. Загрузить значение поля
        OpCodeSize fieldSize = CodeGenUtils.GetSizeForType(field.Type);
        _asm.EmitLoadInd(targetReg, targetReg, fieldSize);
    }

    /// <summary>
    /// Возвращает (StructLayout родительской структуры, FieldInfo поля, тип поля)
    /// </summary>
    private (StructLayout layout, FieldInfo field, string fieldType) ResolveMemberAccessType(MemberAccessNode node)
    {
        string? structType = null;

        if (node.Object is IdentifierNode id)
        {
            // локальная или глобальная переменная
            if (_funcCtx.VarMap.TryGetValue(id.Name, out var loc))
            {
                if (loc.StructTypeName != null)
                    structType = loc.StructTypeName;
                else if (loc.IsPointer && loc.PointedType != null && _structTable.ContainsKey(loc.PointedType))
                    structType = loc.PointedType;
            }
            else if (_globalMem.Contains(id.Name))
            {
                var gInfo = _globalMem.GetInfo(id.Name)!.Value;
                if (_structTable.ContainsKey(gInfo.Type))
                    structType = gInfo.Type;
                else if (gInfo.IsPointer && gInfo.PointedType != null && _structTable.ContainsKey(gInfo.PointedType))
                    structType = gInfo.PointedType;
            }

            if (structType == null)
                ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_NonStructAccess, id.Name);
        }
        else if (node.Object is MemberAccessNode innerMember)
        {
            // рекурсивно получаем тип поля внутреннего доступа
            var (_, _, innerFieldType) = ResolveMemberAccessType(innerMember);
            structType = innerFieldType; // тип поля, к которому обращаемся дальше
        }
        else if (node.Object is ArrayAccessNode arrAcc)
        {
            structType = ResolveArrayStructType(arrAcc.ArrayName);
        }
        else if (node.Object is DereferenceNode)
        {
            ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UnknownStructType, "Dereference in member access not yet supported");
        }
        else
        {
            ThrowHelper.ThrowMiniC(ErrorCode.NotSupported, "Unsupported object for member access");
        }

        if (structType == null)
            ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UnknownStructType, "Cannot determine struct type for member access");

        if (!_structTable.TryGetValue(structType, out var layout))
            ThrowHelper.ThrowMiniC(ErrorCode.NotImplemented, structType);

        var field = layout.GetField(node.FieldName)
                    ?? ThrowHelper.ThrowMiniC<FieldInfo>(ErrorCode.CodeGen_UnknownField, node.FieldName, structType);

        return (layout, field, field.Type);
    }

    public string? ResolveArrayStructType(string arrayName)
    {
        if (_funcCtx.VarMap.TryGetValue(arrayName, out var loc))
        {
            if (loc.IsPointer && loc.PointedType != null && _structTable.ContainsKey(loc.PointedType))
                return loc.PointedType;
            if (loc.StructTypeName != null)
                return loc.StructTypeName;
        }
        else if (_globalMem.Contains(arrayName))
        {
            var gInfo = _globalMem.GetInfo(arrayName)!.Value;
            if (gInfo.IsPointer && gInfo.PointedType != null && _structTable.ContainsKey(gInfo.PointedType))
                return gInfo.PointedType;
            if (_structTable.ContainsKey(gInfo.Type))
                return gInfo.Type;
        }
        ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_NonIndexableType, arrayName);
        return null;
    }

    public void LoadArrayElementToR0(string arrayName, ASTNode indexExpr, RegType targetReg)
    {
        if (_funcCtx.VarMap.TryGetValue(arrayName, out var loc))
        {
            if (loc.IsArray)
                LoadArrayElementFromLocal(loc, indexExpr, targetReg);
            else if (loc.IsPointer)
                LoadPointerElementToR0(arrayName, loc, indexExpr, targetReg);
            else
                ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_NonIndexableType, arrayName);
        }
        else if (_globalMem.Contains(arrayName))
        {
            var gInfo = _globalMem.GetInfo(arrayName)!.Value;
            if (gInfo.IsArray)
                LoadArrayElementFromGlobal(gInfo, arrayName, indexExpr, targetReg);
            else if (gInfo.IsPointer)
                LoadPointerElementToR0(arrayName, null, indexExpr, targetReg);
            else
                ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_NonIndexableType, arrayName);
        }
        else
            ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UndefinedVariable, arrayName);
    }

    public void StoreR0ToArrayElement(string arrayName, ASTNode indexExpr)
    {
        // value уже в r1
        int slot = SaveRegToStack(RegType.r0);

        if (_funcCtx.VarMap.TryGetValue(arrayName, out var loc))
        {
            if (loc.IsArray)
                StoreArrayElementToLocal(loc, indexExpr, slot);
            else if (loc.IsPointer)
                StorePointerElement(arrayName, loc, indexExpr, slot);
            else
                ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_NonIndexableType, arrayName);
        }
        else if (_globalMem.Contains(arrayName))
        {
            var gInfo = _globalMem.GetInfo(arrayName)!.Value;
            if (gInfo.IsArray)
                StoreArrayElementToGlobal(gInfo, arrayName, indexExpr, slot);
            else if (gInfo.IsPointer)
                StorePointerElement(arrayName, null, indexExpr, slot);
            else
                ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_NonIndexableType, arrayName);
        }
        else
            ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UndefinedVariable, arrayName);

        ReleaseSaveSlot();
    }
    private void LoadArrayElementFromLocal(VarLocation loc, ASTNode indexExpr, RegType targetReg)
    {
        var typeSize = loc.TypeSize;
        GenerateExpression(indexExpr, targetReg);
        if (typeSize != OpCodeSize.S8)
            _asm.EmitMultiplyByConstant(targetReg, CodeGenUtils.GetSizeInBytes(typeSize));
        _asm.EmitLdi(CodeGenUtils.TMP_REG_EN, (ulong)loc.StackOffset);
        _asm.EmitAdd(CodeGenUtils.TMP_REG_EN, RegType.rSP);
        _asm.EmitAdd(CodeGenUtils.TMP_REG_EN, targetReg);
        _asm.EmitLoadInd(targetReg, CodeGenUtils.TMP_REG_EN, typeSize);
    }
    private void LoadArrayElementFromGlobal(GlobalInfo gInfo, string arrayName, ASTNode indexExpr, RegType targetReg)
    {
        var typeSize = CodeGenUtils.GetSizeForType(gInfo.Type);
        GenerateExpression(indexExpr, targetReg);
        if (typeSize != OpCodeSize.S8)
            _asm.EmitMultiplyByConstant(targetReg, CodeGenUtils.GetSizeInBytes(typeSize));

        // БЫЛО: _asm.EmitInstruction64(EncodeLDI(TMP_REG), gInfo.Address);
        string label = GlobalMemoryManager.GetLabel(arrayName);
        _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, label);

        _asm.EmitAdd(CodeGenUtils.ADDR_SCRATCH_REG_EN, targetReg);
        _asm.EmitLoadInd(targetReg, CodeGenUtils.ADDR_SCRATCH_REG_EN, typeSize);
    }

    private void StoreArrayElementToGlobal(GlobalInfo gInfo, string arrayName, ASTNode indexExpr, int slot)
    {
        var typeSize = CodeGenUtils.GetSizeForType(gInfo.Type);

        GenerateExpression(indexExpr, RegType.r0);
        if (typeSize != OpCodeSize.S8)
            _asm.EmitMultiplyByConstant(RegType.r0, CodeGenUtils.GetSizeInBytes(typeSize));

        string label = GlobalMemoryManager.GetLabel(arrayName);
        _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, label);
        _asm.EmitAdd(CodeGenUtils.ADDR_SCRATCH_REG_EN, RegType.r0);

        _asm.EmitLdi(RegType.r3, (ulong)slot);
        _asm.EmitAdd(RegType.r3, RegType.rSP);
        _asm.EmitLoadInd(RegType.r1, RegType.r3, OpCodeSize.S64);

        _asm.EmitStoreInd(RegType.r1, CodeGenUtils.ADDR_SCRATCH_REG_EN, typeSize);
    }
    private void LoadPointerElementToR0(string pointerName, VarLocation? loc, ASTNode indexExpr, RegType targetReg)
    {
        // Получаем размер pointed типа
        OpCodeSize pointedSize;
        if (loc != null)
            pointedSize = CodeGenUtils.GetSizeForType(loc.PointedType!);
        else if (_globalMem.Contains(pointerName))
            pointedSize = CodeGenUtils.GetSizeForType(_globalMem.GetInfo(pointerName)!.Value.PointedType!);
        else pointedSize = CodeGenUtils.GetSizeForType("expection type");
        // 1. Вычисляем индекс → r1
        GenerateExpression(indexExpr, RegType.r2);           // index → r3 (scratch)
        _varAccessor.LoadToRegister(pointerName, targetReg); // r1 = pointer value (использует TMP_REG=r27)
        if (pointedSize != OpCodeSize.S8)
            _asm.EmitMultiplyByConstant(RegType.r2, CodeGenUtils.GetSizeInBytes(pointedSize));
        _asm.EmitAdd(targetReg, RegType.r2);                 // r1 = pointer + offset
        _asm.EmitLoadInd(targetReg, targetReg, pointedSize); // result → targetReg
    }

    private void StoreArrayElementToLocal(VarLocation loc, ASTNode indexExpr, int slot)
    {
        var typeSize = loc.TypeSize;

        // 1) index → r1, масштаб, + базовый адрес → r27
        GenerateExpression(indexExpr, RegType.r0);
        if (typeSize != OpCodeSize.S8)
            _asm.EmitMultiplyByConstant(RegType.r0, CodeGenUtils.GetSizeInBytes(typeSize));
        _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)loc.StackOffset);
        _asm.EmitAdd(CodeGenUtils.ADDR_SCRATCH_REG_EN, RegType.rSP);
        _asm.EmitAdd(CodeGenUtils.ADDR_SCRATCH_REG_EN, RegType.r0);
        // ADDR_SCRATCH_REG_EN = &arr[index]

        // 2) value из save-слота → r2
        _asm.EmitLdi(RegType.r3, (ulong)slot);
        _asm.EmitAdd(RegType.r3, RegType.rSP);
        _asm.EmitLoadInd(RegType.r1, RegType.r3, OpCodeSize.S64);

        // 3) STORE
        _asm.EmitStoreInd(RegType.r1, CodeGenUtils.ADDR_SCRATCH_REG_EN, typeSize);
    }
    private void StorePointerElement(string pointerName, VarLocation? loc, ASTNode indexExpr, int slot)
    {
        OpCodeSize pointedSize;
        if (loc != null)
            pointedSize = CodeGenUtils.GetSizeForType(loc.PointedType!);
        else if (_globalMem.Contains(pointerName))
            pointedSize = CodeGenUtils.GetSizeForType(_globalMem.GetInfo(pointerName)!.Value.PointedType!);
        else
            pointedSize = CodeGenUtils.GetSizeForType("expection type");

        // 1) index → r3
        GenerateExpression(indexExpr, RegType.r2);

        // 2) pointer value → r1
        _varAccessor.LoadToRegister(pointerName, RegType.r0);   // r1 = *pointer

        // 3) scale
        if (pointedSize != OpCodeSize.S8)
            _asm.EmitMultiplyByConstant(RegType.r2, CodeGenUtils.GetSizeInBytes(pointedSize));

        // 4) r1 = pointer + offset
        _asm.EmitAdd(RegType.r0, RegType.r2);

        // 5) value из save-слота → r2
        _asm.EmitLdi(RegType.r3, (ulong)slot);
        _asm.EmitAdd(RegType.r3, RegType.rSP);
        _asm.EmitLoadInd(RegType.r1, RegType.r3, OpCodeSize.S64);

        // 6) STORE
        _asm.EmitStoreInd(RegType.r1, RegType.r0, pointedSize);
    }
    public void GenerateCondition(ASTNode condition, string? trueLabel, string? falseLabel)
    {
        if (condition is BinaryOpNode logical && (logical.Operator == "&&" || logical.Operator == "||"))
        {
            if (logical.Operator == "&&")
            {
                GenerateCondition(logical.Left, null, falseLabel);
                GenerateCondition(logical.Right, trueLabel, falseLabel);
            }
            else
            {
                GenerateCondition(logical.Left, trueLabel, null);
                GenerateCondition(logical.Right, trueLabel, falseLabel);
            }
            return;
        }

        if (condition is BinaryOpNode binop && CodeGenUtils.IsComparisonOperator(binop.Operator))
        {
            GenerateExpression(binop.Left, RegType.r0);
            int slot = SaveRegToStack(RegType.r0);
            GenerateExpression(binop.Right, RegType.r0);
            RestoreRegFromStack(slot, RegType.r1);
            _asm.EmitCmp(RegType.r1, RegType.r0);
            GenerateComparisonJump(binop.Operator, trueLabel, falseLabel);
        }
        else
        {
            GenerateExpression(condition);                                      // r0 = значение
                                                                                // Сравнение с нулём: CMP r0, 0 (через r1 как scratch)
            _asm.EmitLdi(RegType.r1, 0);
            _asm.EmitCmp(RegType.r0, RegType.r1);

            if (trueLabel != null && falseLabel != null)
            {
                _asm.EmitJnz(trueLabel);
                _asm.EmitJmp(falseLabel);
            }
            else if (trueLabel != null)
            {
                _asm.EmitJnz(trueLabel);
            }
            else if (falseLabel != null)
            {
                _asm.EmitJz(falseLabel);
            }
        }
    }
    private void GenerateComparisonJump(string op, string? trueLabel, string? falseLabel)
    {
        if (trueLabel != null && falseLabel == null)
        {
            string skip = _getLabel();
            GenerateComparisonJump(op, trueLabel, skip);
            _asm.MarkLabel(skip);
            return;
        }
        if (falseLabel != null && trueLabel == null)
        {
            string skip = _getLabel();
            GenerateComparisonJump(op, skip, falseLabel);
            _asm.MarkLabel(skip);
            return;
        }
        if (trueLabel == null && falseLabel == null)
            return;

        switch (op)
        {
            case "==": _asm.EmitJz(trueLabel!); _asm.EmitJmp(falseLabel!); break;
            case "!=": _asm.EmitJnz(trueLabel!); _asm.EmitJmp(falseLabel!); break;
            case "<": _asm.EmitJl(trueLabel!); _asm.EmitJmp(falseLabel!); break;
            case ">":
                _asm.EmitJl(falseLabel!);
                _asm.EmitJz(falseLabel!);
                _asm.EmitJmp(trueLabel!);
                break;
            case "<=":
                _asm.EmitJl(trueLabel!);
                _asm.EmitJz(trueLabel!);
                _asm.EmitJmp(falseLabel!);
                break;
            case ">=":
                _asm.EmitJl(falseLabel!);
                _asm.EmitJmp(trueLabel!);
                break;
        }
    }

    private void GenerateLogicalAndOr(BinaryOpNode binop)
    {
        string endLabel = _getLabel();
        string trueLabel = _getLabel();
        string falseLabel = _getLabel();

        GenerateExpression(binop.Left);
        _asm.EmitInstR(OpCode.TEST, RegType.r0, RegType.r0);   // flags = r0 & r0
        if (binop.Operator == "&&") _asm.EmitJz(falseLabel);
        else _asm.EmitJnz(trueLabel);

        GenerateExpression(binop.Right);
        _asm.EmitInstR(OpCode.TEST, RegType.r0, RegType.r0);
        if (binop.Operator == "&&") _asm.EmitJz(falseLabel);
        else _asm.EmitJnz(trueLabel);

        // ни одна ветка не сработала — результат "истина" для &&, "ложь" для ||
        _asm.EmitLdi(RegType.r0, binop.Operator == "&&" ? 1UL : 0UL);
        _asm.EmitJmp(endLabel);

        _asm.MarkLabel(trueLabel);
        _asm.EmitLdi(RegType.r0, 1);
        _asm.EmitJmp(endLabel);

        _asm.MarkLabel(falseLabel);
        _asm.EmitLdi(RegType.r0, 0);

        _asm.MarkLabel(endLabel);
    }
    private void EmitBinaryOperation(string op, RegType dest, RegType src)
    {
        switch (op)
        {
            case "+": _asm.EmitAdd(dest, src); break;
            case "-": _asm.EmitSub(dest, src); break;
            case "*": _asm.EmitMultInt(dest, src); break;
            case "/": _asm.EmitDiv(dest, src); break;
            case "&": _asm.EmitAnd(dest, src); break;
            case "|": _asm.EmitOr(dest, src); break;
            case "^": _asm.EmitXOr(dest, src); break;
            case "<<": _asm.EmitShl(dest, src); break;
            case ">>": _asm.EmitShr(dest, src); break;
            default: ThrowHelper.ThrowMiniC(ErrorCode.NotSupported, $"Unsupported operator in EmitBinaryOperation: {op}"); break;
        }
    }
    public static long ComputeConstant(long value1, long value2, string op)
    {
        long result = op switch
        {
            "+" => value1 + value2,
            "-" => value1 - value2,
            "*" => value1 * value2,
            "/" => ComputeDiv(value1, value2),
            "&" => value1 & value2,
            "|" => value1 | value2,
            "^" => value1 ^ value2,
            "<<" => value1 << (int)value2,
            ">>" => value1 >> (int)value2,
            _ => ThrowHelper.ThrowMiniC<long>(ErrorCode.NotSupported, $"Unsupported operator: {op}"),
        };
        return result;
    }

    private static long ComputeDiv(long value1, long value2) => value2 != 0 ? value1 / value2
            : ThrowHelper.ThrowMiniC<long>(ErrorCode.NotSupported, $"Div on zero");

    public void GenerateComparison(string op)
    {
        _asm.EmitSub(RegType.r1, RegType.r0);
        _asm.EmitMov(RegType.r0, RegType.r1);
        _asm.EmitCmp(RegType.r1, RegType.r0);
        string trueLabel = _getLabel();
        string endLabel = _getLabel();
        _asm.EmitLdi(RegType.r0, 0);

        switch (op)
        {
            case "<":
                _asm.EmitMov(RegType.r1, RegType.rFL);
                _asm.EmitLdi(RegType.r2, 2);
                _asm.EmitAnd(RegType.r1, RegType.r2);
                _asm.EmitJnz(trueLabel);
                _asm.EmitJmp(endLabel);
                _asm.MarkLabel(trueLabel);
                _asm.EmitLdi(RegType.r0, 1);
                _asm.MarkLabel(endLabel);
                break;
            case ">":
                _asm.EmitMov(RegType.r1, RegType.rFL);
                _asm.EmitLdi(RegType.r2, 3);
                _asm.EmitAnd(RegType.r1, RegType.r2);
                _asm.EmitJz(trueLabel);
                _asm.EmitJmp(endLabel);
                _asm.MarkLabel(trueLabel);
                _asm.EmitLdi(RegType.r0, 1);
                _asm.MarkLabel(endLabel);
                break;
            case "<=":
                _asm.EmitMov(RegType.r1, RegType.rFL);
                _asm.EmitLdi(RegType.r2, 3);
                _asm.EmitAnd(RegType.r1, RegType.r2);
                _asm.EmitJnz(trueLabel);
                _asm.EmitJmp(endLabel);
                _asm.MarkLabel(trueLabel);
                _asm.EmitLdi(RegType.r0, 1);
                _asm.MarkLabel(endLabel);
                break;
            case ">=":
                _asm.EmitMov(RegType.r1, RegType.rFL);
                _asm.EmitLdi(RegType.r2, 2);
                _asm.EmitAnd(RegType.r1, RegType.r2);
                _asm.EmitJz(trueLabel);
                _asm.EmitJmp(endLabel);
                _asm.MarkLabel(trueLabel);
                _asm.EmitLdi(RegType.r0, 1);
                _asm.MarkLabel(endLabel);
                break;
            case "==":
                _asm.EmitMov(RegType.r1, RegType.rFL);
                _asm.EmitLdi(RegType.r2, 1);
                _asm.EmitAnd(RegType.r1, RegType.r2);
                _asm.EmitJnz(trueLabel);
                _asm.EmitJmp(endLabel);
                _asm.MarkLabel(trueLabel);
                _asm.EmitLdi(RegType.r0, 1);
                _asm.MarkLabel(endLabel);
                break;
            case "!=":
                _asm.EmitMov(RegType.r1, RegType.rFL);
                _asm.EmitLdi(RegType.r2, 1);
                _asm.EmitAnd(RegType.r1, RegType.r2);
                _asm.EmitJz(trueLabel);
                _asm.EmitJmp(endLabel);
                _asm.MarkLabel(trueLabel);
                _asm.EmitLdi(RegType.r0, 1);
                _asm.MarkLabel(endLabel);
                break;
            default:
                ThrowHelper.ThrowMiniC(ErrorCode.NotImplemented, op);
                return;
        }
    }

    private void GenerateAddressOf(AddressOfNode addrOf, RegType targetReg)
    {
        if (addrOf.Operand is IdentifierNode id)
        {
            string varName = id.Name;
            if (_funcCtx.VarMap.TryGetValue(varName, out var loc))
            {
                if (loc.IsRegister)
                    ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_CannotGetRegisterAddress, varName);
                _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)loc.StackOffset);
                _asm.EmitAdd(CodeGenUtils.ADDR_SCRATCH_REG_EN, RegType.rSP);
                _asm.EmitMov(targetReg, CodeGenUtils.ADDR_SCRATCH_REG_EN);
            }
            else if (_globalMem.Contains(varName))
            {
                string label = GlobalMemoryManager.GetLabel(varName);
                _asm.EmitLdi(targetReg, label);
            }
            else
                ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UndefinedVariable, varName);
        }
        else
            ThrowHelper.ThrowMiniC(ErrorCode.NotSupported, "Address-of only supports simple variables currently");
    }

    private void GenerateDereference(DereferenceNode deref, RegType targetReg)
    {
        GenerateExpression(deref.Operand);
        OpCodeSize size = GetPointedSize(deref.Operand);
        _asm.EmitLoadInd(targetReg, targetReg, size);
    }

    private OpCodeSize GetPointedSize(ASTNode expr)
    {
        if (expr is IdentifierNode id)
        {
            if (_funcCtx.VarMap.TryGetValue(id.Name, out var loc) && loc.IsPointer)
                return CodeGenUtils.GetSizeForType(loc.PointedType!);
            if (_globalMem.Contains(id.Name))
            {
                var gInfo = _globalMem.GetInfo(id.Name)!.Value;
                if (gInfo.IsPointer)
                    return CodeGenUtils.GetSizeForType(gInfo.PointedType!);
            }
        }
        return CodeGenUtils.GetSizeForType($"Cannot determine pointed type for dereference of '{expr}'");

    }

    private void GenerateFunctionCall(FunctionCallNode call)
    {
        if (!_functionTable.TryGetValue(call.Name, out var targetFunc))
            ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UnknownFunction, call.Name);
        if (call.Arguments.Count != targetFunc.Parameters.Count)
            ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_ArgumentCountMismatch, call.Name);

        // 1) args 4+ → pseudo-globals (r0..r3 пусты, можно свободно)
        for (int i = 4; i < call.Arguments.Count; i++)
        {
            GenerateExpression(call.Arguments[i]);         // → r0
            var param = targetFunc.Parameters[i];
            string globalName = $"__param_{targetFunc.Name}_{param.Name}";
            OpCodeSize opSize = param.IsPointer ? OpCodeSize.S64 : CodeGenUtils.GetSizeForType(param.Type);
            string label = GlobalMemoryManager.GetLabel(globalName);
            _asm.EmitStore(RegType.r0, opSize, label);
        }

        // 2) args 0..3 — генерируем, сохраняем offset
        int regCount = Math.Min(call.Arguments.Count, 4);
        var slots = new int[regCount];

        for (int i = 0; i < regCount; i++)
        {
            var param = targetFunc.Parameters[i];
            if (_structTable.ContainsKey(param.Type))
                ThrowHelper.ThrowMiniC(ErrorCode.NotSupported, "Struct parameters via registers not yet supported");
            GenerateExpression(call.Arguments[i], (RegType)i);
            slots[i] = SaveRegToStack((RegType)i);   // ← сохраняем возвращаемый offset
        }

        // 3) восстанавливаем по сохранённым offset'ам
        for (int i = 0; i < regCount; i++)
            RestoreRegFromStack(slots[i], (RegType)i);

        _asm.EmitCall($"func_{call.Name}");
    }
}