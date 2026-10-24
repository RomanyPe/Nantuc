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
                _asm.EmitLdi(RegType.r1, (ulong)num.Value);
                break;
            case IdentifierNode id:
                _varAccessor.LoadToRegister(id.Name, RegType.r1);
                break;
            case BinaryOpNode binop:
                GenerateBinaryOp(binop, RegType.r1);
                break;
            case UnaryOpNode unop:
                GenerateUnaryOp(unop);
                break;
            case FunctionCallNode call:
                GenerateFunctionCall(call);
                break;
            case ArrayAccessNode arrAcc:
                LoadArrayElementToR0(arrAcc.ArrayName, arrAcc.Index);
                break;
            case AddressOfNode addrOf:
                GenerateAddressOf(addrOf);
                break;
            case DereferenceNode deref:
                GenerateDereference(deref);
                break;
            case MemberAccessNode member:
                GenerateMemberAccess(member);
                break;
            default:
                ThrowHelper.ThrowMiniC(ErrorCode.NotSupported, $"Unsupported expression: {expr.GetType()}");
                break;
        }
    }

    public void GenerateExpression(ASTNode expr, RegType targetReg)
    {
        if (targetReg == RegType.r1)
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
                _varAccessor.LoadToRegister(id.Name, targetReg);
                break;
            case BinaryOpNode binop:
                GenerateBinaryOp(binop, targetReg);
                break;
            case UnaryOpNode unop:
                GenerateUnaryOp(unop, targetReg);
                break;
            default:
                GenerateExpression(expr);
                if (targetReg != RegType.r1)
                    _asm.EmitMov(targetReg, RegType.r1);
                break;
        }
    }

    private void GenerateBinaryOp(BinaryOpNode binop, RegType targetReg)
    {
        // обработка логических &&, || без изменений
        if (binop.Operator == "&&" || binop.Operator == "||")
        {
            GenerateLogicalAndOr(binop); // результат всегда в r0
            if (targetReg != RegType.r1)
                _asm.EmitMov(targetReg, RegType.r1);
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
            RegType tempReg = (targetReg != RegType.r2) ? RegType.r2 : RegType.r3;
            GenerateExpression(binop.Right, tempReg);
            EmitBinaryOperation(binop.Operator, targetReg, tempReg);
            return;
        }

        // Попытка оптимизации: targetReg совпадает с регистром правого операнда (для коммутативных операций)
        if (binop.Right is IdentifierNode rightId && _varAccessor.TryGetRegister(rightId.Name, out var rightReg) && rightReg == targetReg
            && (binop.Operator == "+" || binop.Operator == "*")) // только коммутативные
        {
            RegType tempReg = (targetReg != RegType.r1) ? RegType.r1 : RegType.r2;
            GenerateExpression(binop.Left, tempReg);
            EmitBinaryOperation(binop.Operator, targetReg, tempReg);
            return;
        }

        // Общий случай
        GenerateExpression(binop.Left, targetReg);
        RegType rReg = (targetReg != RegType.r2) ? RegType.r2 : RegType.r3;
        RegType addrReg = RegType.r4;

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
                    RegType scratch = targetReg == RegType.r2 ? RegType.r3 : RegType.r2;
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
    public void GenerateMemberAddress(MemberAccessNode node)
    {
        string? structType = null;
        switch (node.Object)
        {
            case IdentifierNode identifer:
                _varAccessor.LoadAddressToRegister(identifer.Name, RegType.r1);
                return;
            case MemberAccessNode node1:
                {
                    // Рекурсивно вычисляем адрес вложенного объекта (результат в r0)
                    GenerateMemberAddress(node1);

                    // Получаем ТИП поля node1, чтобы понять, в какой структуре искать текущее поле
                    var (_, _, innerFieldType) = ResolveMemberAccessType(node1);

                    if (!_structTable.TryGetValue(innerFieldType, out var innerLayout))
                        ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UnknownStructType, $"Unknown struct type '{innerFieldType}'", innerFieldType);

                    var currentField = innerLayout.GetField(node.FieldName)
                                       ?? ThrowHelper.ThrowMiniC<FieldInfo>(ErrorCode.CodeGen_UnknownField, node.FieldName, innerFieldType);

                    if (currentField.Offset > 0)
                    {
                        _asm.EmitLdi(CodeGenUtils.TMP_REG_EN, (ulong)currentField.Offset);
                        _asm.EmitAdd(RegType.r1, CodeGenUtils.TMP_REG_EN);
                    }
                    return;
                }

            case DereferenceNode:
                ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UnknownStructType, "Dereference member access not implemented yet");
                return;
            case ArrayAccessNode arrAcc:
                {
                    // arr[i].field — адрес поля
                    string? arrStructType = ResolveArrayStructType(arrAcc.ArrayName);
                    var layoutstr = _structTable[arrStructType!];
                    var fieldstr = layoutstr.GetField(node.FieldName)
                                    ?? ThrowHelper.ThrowMiniC<FieldInfo>(ErrorCode.CodeGen_UnknownField, node.FieldName, arrStructType!);

                    // Вычисляем адрес элемента arr[i]
                    GenerateExpression(arrAcc.Index);
                    _asm.EmitMov(RegType.r3, RegType.r1);
                    _varAccessor.LoadToRegister(arrAcc.ArrayName, RegType.r1);
                    int elemSize = layoutstr.Size;
                    if (elemSize > 1)
                        _asm.EmitMultiplyByConstant(RegType.r3, elemSize);
                    _asm.EmitAdd(RegType.r1, RegType.r3);
                    // r0 = адрес arr[i]

                    // Прибавляем смещение поля
                    if (fieldstr.Offset > 0)
                    {
                        _asm.EmitLdi(CodeGenUtils.TMP_REG_EN, (ulong)fieldstr.Offset);
                        _asm.EmitAdd(RegType.r1, CodeGenUtils.TMP_REG_EN);
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
            GenerateExpression(node.Object);  // r0 = ptr
        }
        else
        {
            if (node.Object is IdentifierNode identifer)
                _varAccessor.LoadToRegister(identifer.Name, RegType.r1);
            else ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_InvalidDotAccess, "Dot address only for simple variables");
        }
        if (field.Offset > 0)
        {
            _asm.EmitLdi(CodeGenUtils.TMP_REG_EN, (ulong)field.Offset);
            _asm.EmitAdd(RegType.r1, CodeGenUtils.TMP_REG_EN);
        }
    }


    private void GenerateMemberAccess(MemberAccessNode node)
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
                    GenerateMemberAddress(node); // этот метод вычислит адрес поля
                                                 // Теперь r0 содержит адрес поля, загружаем значение
                    var (_, fieldMem, _) = ResolveMemberAccessType(node);
                    OpCodeSize fieldSizeMem = CodeGenUtils.GetSizeForType(fieldMem.Type);
                    _asm.EmitLoadInd(RegType.r1, RegType.r1, fieldSizeMem);
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

                    GenerateExpression(arrAcc.Index);
                    _asm.EmitMov(RegType.r3, RegType.r1);
                    _varAccessor.LoadToRegister(arrAcc.ArrayName, RegType.r1);
                    int elemSize = layoutstr.Size;
                    if (elemSize > 1)
                        _asm.EmitMultiplyByConstant(RegType.r3, elemSize);
                    _asm.EmitAdd(RegType.r1, RegType.r3);

                    if (fieldstr.Offset > 0)
                    {
                        _asm.EmitLdi(CodeGenUtils.TMP_REG_EN, (ulong)fieldstr.Offset);
                        _asm.EmitAdd(RegType.r1, CodeGenUtils.TMP_REG_EN);
                    }

                    // Загружаем значение поля
                    OpCodeSize fieldSizestr = CodeGenUtils.GetSizeForType(fieldstr.Type);
                    _asm.EmitLoadInd(RegType.r1, RegType.r1, fieldSizestr);
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
            GenerateExpression(node.Object);
        }
        else
        {
            if (node.Object is IdentifierNode identifer)
            {
                _varAccessor.LoadAddressToRegister(identifer.Name, RegType.r1);
            }
            else
            {
                ThrowHelper.ThrowMiniC(ErrorCode.NotSupported, "Dot access only supported for simple variables currently");
            }
        }

        if (field.Offset > 0)
        {
            _asm.EmitLdi(CodeGenUtils.TMP_REG_EN, (ulong)field.Offset);
            _asm.EmitAdd(RegType.r1, CodeGenUtils.TMP_REG_EN);
        }

        // 4. Загрузить значение поля
        OpCodeSize fieldSize = CodeGenUtils.GetSizeForType(field.Type);
        _asm.EmitLoadInd(RegType.r1, RegType.r1, fieldSize);
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

    public void LoadArrayElementToR0(string arrayName, ASTNode indexExpr)
    {
        if (_funcCtx.VarMap.TryGetValue(arrayName, out var loc))
        {
            if (loc.IsArray)
                LoadArrayElementFromLocal(loc, indexExpr);
            else if (loc.IsPointer)
                LoadPointerElementToR0(arrayName, loc, indexExpr);
            else
                ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_NonIndexableType, arrayName);
        }
        else if (_globalMem.Contains(arrayName))
        {
            var gInfo = _globalMem.GetInfo(arrayName)!.Value;
            if (gInfo.IsArray)
                LoadArrayElementFromGlobal(gInfo, arrayName, indexExpr);   // ← новый аргумент
            else if (gInfo.IsPointer)
                LoadPointerElementToR0(arrayName, null, indexExpr);
            else
                ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_NonIndexableType, arrayName);
        }
        else
            ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UndefinedVariable, arrayName);
    }

    public void StoreR0ToArrayElement(string arrayName, ASTNode indexExpr)
    {
        _asm.EmitMov(RegType.r2, RegType.r1);

        if (_funcCtx.VarMap.TryGetValue(arrayName, out var loc))
        {
            if (loc.IsArray)
                StoreArrayElementToLocal(loc, indexExpr);
            else if (loc.IsPointer)
                StorePointerElement(arrayName, loc, indexExpr);
            else
                ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_NonIndexableType, arrayName);
        }
        else if (_globalMem.Contains(arrayName))
        {
            var gInfo = _globalMem.GetInfo(arrayName)!.Value;
            if (gInfo.IsArray)
                StoreArrayElementToGlobal(gInfo, arrayName, indexExpr);
            else if (gInfo.IsPointer)
                StorePointerElement(arrayName, null, indexExpr);
            else
                ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_NonIndexableType, arrayName);
        }
        else
            ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UndefinedVariable, arrayName);
    }

    private void LoadArrayElementFromLocal(VarLocation loc, ASTNode indexExpr)
    {
        var typeSize = loc.TypeSize;
        GenerateExpression(indexExpr);
        if (typeSize != OpCodeSize.S8)
            _asm.EmitMultiplyByConstant(RegType.r1, CodeGenUtils.GetSizeInBytes(typeSize));
        _asm.EmitLdi(CodeGenUtils.TMP_REG_EN, (ulong)loc.StackOffset);
        _asm.EmitAdd(CodeGenUtils.TMP_REG_EN, RegType.rSP);
        _asm.EmitAdd(CodeGenUtils.TMP_REG_EN, RegType.r1);
        _asm.EmitLoadInd(RegType.r1, CodeGenUtils.TMP_REG_EN, typeSize);
    }

    private void LoadArrayElementFromGlobal(GlobalInfo gInfo, string arrayName, ASTNode indexExpr)
    {
        var typeSize = CodeGenUtils.GetSizeForType(gInfo.Type);
        GenerateExpression(indexExpr);
        if (typeSize != OpCodeSize.S8)
            _asm.EmitMultiplyByConstant(RegType.r1, CodeGenUtils.GetSizeInBytes(typeSize));

        // БЫЛО: _asm.EmitInstruction64(EncodeLDI(TMP_REG), gInfo.Address);
        string label = GlobalMemoryManager.GetLabel(arrayName);
        _asm.EmitLdi(CodeGenUtils.TMP_REG_EN, label);

        _asm.EmitAdd(CodeGenUtils.TMP_REG_EN, RegType.r1);
        _asm.EmitLoadInd(RegType.r1, CodeGenUtils.TMP_REG_EN, typeSize);
    }

    private void StoreArrayElementToGlobal(GlobalInfo gInfo, string arrayName, ASTNode indexExpr)
    {
        var typeSize = CodeGenUtils.GetSizeForType(gInfo.Type);
        GenerateExpression(indexExpr);
        if (typeSize != OpCodeSize.S8)
            _asm.EmitMultiplyByConstant(RegType.r1, CodeGenUtils.GetSizeInBytes(typeSize));

        string label = GlobalMemoryManager.GetLabel(arrayName);
        _asm.EmitLdi(CodeGenUtils.TMP_REG_EN, label);

        _asm.EmitAdd(CodeGenUtils.TMP_REG_EN, RegType.r1);
        _asm.EmitStoreInd(RegType.r2, CodeGenUtils.TMP_REG_EN, typeSize);
    }

    private void LoadPointerElementToR0(string pointerName, VarLocation? loc, ASTNode indexExpr)
    {
        // Получаем размер pointed типа
        OpCodeSize pointedSize;
        if (loc != null)
            pointedSize = CodeGenUtils.GetSizeForType(loc.PointedType!);
        else if (_globalMem.Contains(pointerName))
            pointedSize = CodeGenUtils.GetSizeForType(_globalMem.GetInfo(pointerName)!.Value.PointedType!);
        else pointedSize = CodeGenUtils.GetSizeForType("expection type");
        // 1. Вычисляем индекс → r1
        GenerateExpression(indexExpr);   // r0 = индекс
        _asm.EmitMov(RegType.r2, RegType.r1);
        // 2. Загружаем адрес указателя → r0
        _varAccessor.LoadToRegister(pointerName, RegType.r1);   // r0 = адрес
                                                                // 3. Умножаем индекс на размер элемента
        if (pointedSize != OpCodeSize.S8)
            _asm.EmitMultiplyByConstant(RegType.r2, CodeGenUtils.GetSizeInBytes(pointedSize));
        // 4. r0 = адрес + смещение
        _asm.EmitAdd(RegType.r1, RegType.r2);
        // 5. Загружаем значение
        _asm.EmitLoadInd(RegType.r1, RegType.r1, pointedSize);
    }

    private void StoreArrayElementToLocal(VarLocation loc, ASTNode indexExpr)
    {
        var typeSize = loc.TypeSize;
        GenerateExpression(indexExpr);
        if (typeSize != OpCodeSize.S8)
            _asm.EmitMultiplyByConstant(RegType.r1, CodeGenUtils.GetSizeInBytes(typeSize));
        _asm.EmitLdi(CodeGenUtils.TMP_REG_EN, (ulong)loc.StackOffset);
        _asm.EmitAdd(CodeGenUtils.TMP_REG_EN, RegType.rSP);
        _asm.EmitAdd(CodeGenUtils.TMP_REG_EN, RegType.r1);
        _asm.EmitStoreInd(RegType.r2, CodeGenUtils.TMP_REG_EN, typeSize);
    }

    private void StorePointerElement(string pointerName, VarLocation? loc, ASTNode indexExpr)
    {
        OpCodeSize pointedSize;
        if (loc != null)
            pointedSize = CodeGenUtils.GetSizeForType(loc.PointedType!);
        else if (_globalMem.Contains(pointerName))
            pointedSize = CodeGenUtils.GetSizeForType(_globalMem.GetInfo(pointerName)!.Value.PointedType!);
        else
            pointedSize = CodeGenUtils.GetSizeForType("expection type");

        // 1. Вычисляем индекс → r2
        GenerateExpression(indexExpr);
        _asm.EmitMov(RegType.r3, RegType.r1);

        // 2. Умножаем индекс на размер элемента (в r2)
        if (pointedSize != OpCodeSize.S8)
        {
            int elemSize = CodeGenUtils.GetSizeInBytes(pointedSize);
            _asm.EmitMultiplyByConstant( RegType.r3, elemSize);
        }

        // 3. Загружаем адрес указателя → r0
        _varAccessor.LoadToRegister(pointerName, RegType.r1);

        // 4. r0 = адрес + смещение
        _asm.EmitAdd(RegType.r1, RegType.r3);

        // 5. Сохраняем значение из r1 (которое пришло из StoreR0ToArrayElement)
        _asm.EmitStoreInd(RegType.r2, RegType.r1, pointedSize);
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
            GenerateExpression(binop.Left);                                     // r0 = left
            _asm.EmitMov(RegType.r2, RegType.r1);            // r1 = left
            GenerateExpression(binop.Right);                                    // r0 = right
                                                                                // Только CMP обновляет флаги!
            _asm.EmitCmp(RegType.r2, RegType.r1);            // flags = r1 - r0
            GenerateComparisonJump(binop.Operator, trueLabel, falseLabel);
        }
        else
        {
            GenerateExpression(condition);                                      // r0 = значение
                                                                                // Сравнение с нулём: CMP r0, 0 (через r1 как scratch)
            _asm.EmitLdi(RegType.r2, 0);
            _asm.EmitCmp(RegType.r1, RegType.r2);

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
        _asm.EmitCmp(RegType.r1, RegType.r2);
        if (binop.Operator == "&&")
        {
            _asm.EmitJz(falseLabel);
        }
        else
        {
            _asm.EmitJnz(trueLabel);
        }

        GenerateExpression(binop.Right);
        _asm.EmitCmp(RegType.r1, RegType.r2);

        if (binop.Operator == "&&")
        {
            _asm.EmitJz(falseLabel);
        }
        else
        {
            _asm.EmitJnz(trueLabel);
        }

        if (binop.Operator == "&&")
        {
            _asm.EmitLdi(RegType.r1, 1);
        }
        else
        {
            _asm.EmitLdi(RegType.r1, 0);
        }

        _asm.EmitJmp(endLabel);

        _asm.MarkLabel(trueLabel);
        _asm.EmitLdi(RegType.r1, 1);
        _asm.EmitJmp(endLabel);

        _asm.MarkLabel(falseLabel);
        _asm.EmitLdi(RegType.r1, 0);

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
        _asm.EmitSub(RegType.r2, RegType.r1);
        _asm.EmitMov(RegType.r1, RegType.r2);
        _asm.EmitCmp(RegType.r2, RegType.r1);
        string trueLabel = _getLabel();
        string endLabel = _getLabel();
        _asm.EmitLdi(RegType.r1, 0);

        switch (op)
        {
            case "<":
                _asm.EmitMov(RegType.r2, RegType.rFL);
                _asm.EmitLdi(RegType.r3, 2);
                _asm.EmitAnd(RegType.r2, RegType.r3);
                _asm.EmitJnz(trueLabel);
                _asm.EmitJmp(endLabel);
                _asm.MarkLabel(trueLabel);
                _asm.EmitLdi(RegType.r1, 1);
                _asm.MarkLabel(endLabel);
                break;
            case ">":
                _asm.EmitMov(RegType.r2, RegType.rFL);
                _asm.EmitLdi(RegType.r3, 3);
                _asm.EmitAnd(RegType.r2, RegType.r3);
                _asm.EmitJz(trueLabel);
                _asm.EmitJmp(endLabel);
                _asm.MarkLabel(trueLabel);
                _asm.EmitLdi(RegType.r1, 1);
                _asm.MarkLabel(endLabel);
                break;
            case "<=":
                _asm.EmitMov(RegType.r2, RegType.rFL);
                _asm.EmitLdi(RegType.r3, 3);
                _asm.EmitAnd(RegType.r2, RegType.r3);
                _asm.EmitJnz(trueLabel);
                _asm.EmitJmp(endLabel);
                _asm.MarkLabel(trueLabel);
                _asm.EmitLdi(RegType.r1, 1);
                _asm.MarkLabel(endLabel);
                break;
            case ">=":
                _asm.EmitMov(RegType.r2, RegType.rFL);
                _asm.EmitLdi(RegType.r3, 2);
                _asm.EmitAnd(RegType.r2, RegType.r3);
                _asm.EmitJz(trueLabel);
                _asm.EmitJmp(endLabel);
                _asm.MarkLabel(trueLabel);
                _asm.EmitLdi(RegType.r1, 1);
                _asm.MarkLabel(endLabel);
                break;
            case "==":
                _asm.EmitMov(RegType.r2, RegType.rFL);
                _asm.EmitLdi(RegType.r3, 1);
                _asm.EmitAnd(RegType.r2, RegType.r3);
                _asm.EmitJnz(trueLabel);
                _asm.EmitJmp(endLabel);
                _asm.MarkLabel(trueLabel);
                _asm.EmitLdi(RegType.r1, 1);
                _asm.MarkLabel(endLabel);
                break;
            case "!=":
                _asm.EmitMov(RegType.r2, RegType.rFL);
                _asm.EmitLdi(RegType.r3, 1);
                _asm.EmitAnd(RegType.r2, RegType.r3);
                _asm.EmitJz(trueLabel);
                _asm.EmitJmp(endLabel);
                _asm.MarkLabel(trueLabel);
                _asm.EmitLdi(RegType.r1, 1);
                _asm.MarkLabel(endLabel);
                break;
            default:
                ThrowHelper.ThrowMiniC(ErrorCode.NotImplemented, op);
                return;
        }
    }

    private void GenerateUnaryOp(UnaryOpNode unop)
    {
        switch (unop.Operator)
        {
            case "-":
                GenerateExpression(unop.Operand);
                _asm.EmitNot(RegType.r1);
                _asm.EmitInc(RegType.r1);
                break;
            case "!":
                GenerateExpression(unop.Operand);
                string trueLabel = _getLabel();
                string endLabel = _getLabel();
                _asm.EmitLdi(RegType.r1, 1);
                _asm.EmitCmp(RegType.r1, RegType.r2);
                _asm.EmitJnz(trueLabel);
                _asm.EmitLdi(RegType.r1, 0);
                _asm.EmitJmp(endLabel);
                _asm.MarkLabel(trueLabel);
                _asm.EmitLdi(RegType.r1, 1);
                _asm.MarkLabel(endLabel);
                break;
            case "~":
                GenerateExpression(unop.Operand);
                _asm.EmitNot(RegType.r1);
                break;
            default:
                ThrowHelper.ThrowMiniC(ErrorCode.NotSupported, $"Unsupported unary operator: {unop.Operator}");
                return;
        }
    }

    private void GenerateAddressOf(AddressOfNode addrOf)
    {
        if (addrOf.Operand is IdentifierNode id)
        {
            string varName = id.Name;
            if (_funcCtx.VarMap.TryGetValue(varName, out var loc))
            {
                if (loc.IsRegister)
                    ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_CannotGetRegisterAddress, varName);
                _asm.EmitLdi(CodeGenUtils.TMP_REG_EN, (ulong)loc.StackOffset);
                _asm.EmitAdd(CodeGenUtils.TMP_REG_EN, RegType.rSP);
                _asm.EmitMov(RegType.r1, CodeGenUtils.TMP_REG_EN);
            }
            else if (_globalMem.Contains(varName))
            {
                string label = GlobalMemoryManager.GetLabel(varName);
                _asm.EmitLdi(RegType.r1, label);
            }
            else
                ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UndefinedVariable, varName);
        }
        else
            ThrowHelper.ThrowMiniC(ErrorCode.NotSupported, "Address-of only supports simple variables currently");
    }

    private void GenerateDereference(DereferenceNode deref)
    {
        GenerateExpression(deref.Operand);
        OpCodeSize size = GetPointedSize(deref.Operand);
        _asm.EmitLoadInd(RegType.r1, RegType.r1, size);
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

        // Передаём первые 4 аргумента через регистры r0-r3
        int regCount = Math.Min(call.Arguments.Count, 4);
        for (int i = 0; i < regCount; i++)
        {
            var param = targetFunc.Parameters[i];
            if (_structTable.ContainsKey(param.Type))
                ThrowHelper.ThrowMiniC(ErrorCode.NotSupported, "Struct parameters via registers not yet supported");
            // Генерируем выражение в нужный регистр
            GenerateExpression(call.Arguments[i], (RegType)i); // RegType.r0 + i
        }

        // Если параметров больше 4 – временно используем псевдоглобалы
        for (int i = 4; i < call.Arguments.Count; i++)
        {
            GenerateExpression(call.Arguments[i]);
            var param = targetFunc.Parameters[i];
            string globalName = $"__param_{targetFunc.Name}_{param.Name}";
            if (!_globalMem.Contains(globalName))
                ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UndefinedVariable, globalName);

            OpCodeSize opSize = param.IsPointer ? OpCodeSize.S64 : CodeGenUtils.GetSizeForType(param.Type);
            string label = GlobalMemoryManager.GetLabel(globalName);
            _asm.EmitStore(RegType.r1, opSize, label);
        }

        _asm.EmitCall($"func_{call.Name}");
    }}
