using Nantuc.Compiler.ASM;
using Nantuc.Compiler.ASM.Optimizators;
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
                _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(RegType.r1.ToUint()), (ulong)num.Value);
                break;
            case IdentifierNode id:
                _varAccessor.LoadToRegister(id.Name, RegType.r1);
                break;
            case BinaryOpNode binop:
                GenerateBinaryOp(binop);
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
                _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(targetReg.ToUint()), (ulong)num.Value);
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
                    _asm.EmitInstruction(InstructionEncoder.EncodeR(
                        OpCode.MOV.ToUint(), targetReg.ToUint(), RegType.r1.ToUint()));
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
                _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.MOV.ToUint(), targetReg.ToUint(), RegType.r1.ToUint()));
            return;
        }

        // сворачивание констант
        if (binop.Left is NumberNode l && binop.Right is NumberNode r)
        {
            long result = ComputeConstant(l.Value, r.Value, binop.Operator);
            _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(targetReg.ToUint()), (ulong)result);
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

        if (binop.Left is IdentifierNode lid
            && _varAccessor.TryGetRegister(lid.Name, out var lreg)
            && lreg != RegType.r0 && lreg != RegType.r1)
        {
            RegType tmp = (lreg != RegType.r2) ? RegType.r2 : RegType.r3;
            GenerateExpression(binop.Right, tmp);
            EmitBinaryOperation(binop.Operator, lreg, tmp);
            if (lreg != targetReg)
                _asm.EmitInstruction(InstructionEncoder.EncodeR(
                    OpCode.MOV.ToUint(), targetReg.ToUint(), lreg.ToUint()));
            return;
        }
        // Общий случай: левый операнд в targetReg, правый в r1 (или r2 при конфликте)
        GenerateExpression(binop.Left, targetReg);
        RegType rReg = (targetReg != RegType.r2) ? RegType.r2 : RegType.r3;
        GenerateExpression(binop.Right, rReg);
        EmitBinaryOperation(binop.Operator, targetReg, rReg);
    }
    private void GenerateUnaryOp(UnaryOpNode unop, RegType targetReg)
    {
        switch (unop.Operator)
        {
            case "-":
                GenerateExpression(unop.Operand, targetReg);
                _asm.EmitInstruction(InstructionEncoder.EncodeU(OpCode.NOT.ToUint(), targetReg.ToUint()));
                _asm.EmitInstruction(InstructionEncoder.EncodeU(OpCode.INC.ToUint(), targetReg.ToUint()));
                break;

            case "!":
                {
                    GenerateExpression(unop.Operand, targetReg);           // targetReg = operand
                    RegType scratch = targetReg == RegType.r2 ? RegType.r3 : RegType.r2;
                    _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(scratch.ToUint()), 0);
                    _asm.EmitInstruction(InstructionEncoder.EncodeR(
                        OpCode.CMP.ToUint(), targetReg.ToUint(), scratch.ToUint()));   // flags = targetReg - 0

                    string endLabel = _getLabel();
                    _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(targetReg.ToUint()), 0);
                    _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JNZ.ToUint()), endLabel);
                    _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(targetReg.ToUint()), 1);
                    _asm.MarkLabel(endLabel);
                    break;
                }

            case "~":
                GenerateExpression(unop.Operand, targetReg);
                _asm.EmitInstruction(InstructionEncoder.EncodeU(OpCode.NOT.ToUint(), targetReg.ToUint()));
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
                        _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(CodeGenUtils.TMP_REG), (ulong)currentField.Offset);
                        _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), RegType.r1.ToUint(), CodeGenUtils.TMP_REG));
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
                    _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.MOV.ToUint(), RegType.r3.ToUint(), RegType.r1.ToUint()));
                    _varAccessor.LoadToRegister(arrAcc.ArrayName, RegType.r1);
                    int elemSize = layoutstr.Size;
                    if (elemSize > 1)
                        CodeGenUtils.EmitMultiplyByConstant(_asm, RegType.r3.ToUint(), elemSize);
                    _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), RegType.r1.ToUint(), RegType.r3.ToUint()));
                    // r0 = адрес arr[i]

                    // Прибавляем смещение поля
                    if (fieldstr.Offset > 0)
                    {
                        _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(CodeGenUtils.TMP_REG), (ulong)fieldstr.Offset);
                        _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), RegType.r1.ToUint(), CodeGenUtils.TMP_REG));
                    }
                    // Готово: r0 = адрес поля
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
            _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(CodeGenUtils.TMP_REG), (ulong)field.Offset);
            _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), RegType.r1.ToUint(), CodeGenUtils.TMP_REG));
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
                    _asm.EmitInstruction(InstructionEncoder.EncodeLOAD_IND(RegType.r1.ToUint(), RegType.r1.ToUint(), fieldSizeMem.ToUint()));
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
                    _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.MOV.ToUint(), RegType.r3.ToUint(), RegType.r1.ToUint()));
                    _varAccessor.LoadToRegister(arrAcc.ArrayName, RegType.r1);
                    int elemSize = layoutstr.Size;
                    if (elemSize > 1)
                        CodeGenUtils.EmitMultiplyByConstant(_asm, RegType.r3.ToUint(), elemSize);
                    _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), RegType.r1.ToUint(), RegType.r3.ToUint()));

                    if (fieldstr.Offset > 0)
                    {
                        _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(CodeGenUtils.TMP_REG), (ulong)fieldstr.Offset);
                        _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), RegType.r1.ToUint(), CodeGenUtils.TMP_REG));
                    }

                    // Загружаем значение поля
                    OpCodeSize fieldSizestr = CodeGenUtils.GetSizeForType(fieldstr.Type);
                    _asm.EmitInstruction(InstructionEncoder.EncodeLOAD_IND(RegType.r1.ToUint(), RegType.r1.ToUint(), fieldSizestr.ToUint()));
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
            _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(CodeGenUtils.TMP_REG), (ulong)field.Offset);
            _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), RegType.r1.ToUint(), CodeGenUtils.TMP_REG));
        }

        // 4. Загрузить значение поля
        OpCodeSize fieldSize = CodeGenUtils.GetSizeForType(field.Type);
        _asm.EmitInstruction(InstructionEncoder.EncodeLOAD_IND(RegType.r1.ToUint(), RegType.r1.ToUint(), fieldSize.ToUint()));
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
                LoadArrayElementFromGlobal(gInfo, indexExpr);
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
        _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.MOV.ToUint(), RegType.r2.ToUint(), RegType.r1.ToUint()));

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
                StoreArrayElementToGlobal(gInfo, indexExpr);
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
            CodeGenUtils.EmitMultiplyByConstant(_asm, RegType.r1.ToUint(), CodeGenUtils.GetSizeInBytes(typeSize));
        _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(CodeGenUtils.TMP_REG), (ulong)loc.StackOffset);
        _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), CodeGenUtils.TMP_REG, RegType.rSP.ToUint()));
        _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), CodeGenUtils.TMP_REG, RegType.r1.ToUint()));
        _asm.EmitInstruction(InstructionEncoder.EncodeLOAD_IND(RegType.r1.ToUint(), CodeGenUtils.TMP_REG, typeSize.ToUint()));
    }

    private void LoadArrayElementFromGlobal(GlobalInfo gInfo, ASTNode indexExpr)
    {
        var typeSize = CodeGenUtils.GetSizeForType(gInfo.Type);
        GenerateExpression(indexExpr);
        if (typeSize != OpCodeSize.S8)
            CodeGenUtils.EmitMultiplyByConstant(_asm, RegType.r1.ToUint(), CodeGenUtils.GetSizeInBytes(typeSize));
        _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(CodeGenUtils.TMP_REG), gInfo.Address);
        _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), CodeGenUtils.TMP_REG, RegType.r1.ToUint()));
        _asm.EmitInstruction(InstructionEncoder.EncodeLOAD_IND(RegType.r1.ToUint(), CodeGenUtils.TMP_REG, typeSize.ToUint()));
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
        _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.MOV.ToUint(), RegType.r2.ToUint(), RegType.r1.ToUint()));
        // 2. Загружаем адрес указателя → r0
        _varAccessor.LoadAddressToRegister(pointerName, RegType.r1);   // r0 = адрес
                                         // 3. Умножаем индекс на размер элемента
        if (pointedSize != OpCodeSize.S8)
            CodeGenUtils.EmitMultiplyByConstant(_asm, RegType.r2.ToUint(), CodeGenUtils.GetSizeInBytes(pointedSize));
        // 4. r0 = адрес + смещение
        _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), RegType.r1.ToUint(), RegType.r2.ToUint()));
        // 5. Загружаем значение
        _asm.EmitInstruction(InstructionEncoder.EncodeLOAD_IND(RegType.r1.ToUint(), RegType.r1.ToUint(), pointedSize.ToUint()));
    }

    private void StoreArrayElementToLocal(VarLocation loc, ASTNode indexExpr)
    {
        var typeSize = loc.TypeSize;
        GenerateExpression(indexExpr);
        if (typeSize != OpCodeSize.S8)
            CodeGenUtils.EmitMultiplyByConstant(_asm, RegType.r1.ToUint(), CodeGenUtils.GetSizeInBytes(typeSize));
        _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(CodeGenUtils.TMP_REG), (ulong)loc.StackOffset);
        _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), CodeGenUtils.TMP_REG, RegType.rSP.ToUint()));
        _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), CodeGenUtils.TMP_REG, RegType.r1.ToUint()));
        _asm.EmitInstruction(InstructionEncoder.EncodeSTORE_IND(RegType.r2.ToUint(), CodeGenUtils.TMP_REG, typeSize.ToUint()));
    }

    private void StoreArrayElementToGlobal(GlobalInfo gInfo, ASTNode indexExpr)
    {
        var typeSize = CodeGenUtils.GetSizeForType(gInfo.Type);
        GenerateExpression(indexExpr);
        if (typeSize != OpCodeSize.S8)
            CodeGenUtils.EmitMultiplyByConstant(_asm, RegType.r1.ToUint(), CodeGenUtils.GetSizeInBytes(typeSize));
        _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(CodeGenUtils.TMP_REG), gInfo.Address);
        _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), CodeGenUtils.TMP_REG, RegType.r1.ToUint()));
        _asm.EmitInstruction(InstructionEncoder.EncodeSTORE_IND(RegType.r2.ToUint(), CodeGenUtils.TMP_REG, typeSize.ToUint()));
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
        _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.MOV.ToUint(), RegType.r3.ToUint(), RegType.r1.ToUint()));

        // 2. Умножаем индекс на размер элемента (в r2)
        if (pointedSize != OpCodeSize.S8)
        {
            int elemSize = CodeGenUtils.GetSizeInBytes(pointedSize);
            CodeGenUtils.EmitMultiplyByConstant(_asm, RegType.r3.ToUint(), elemSize);
        }

        // 3. Загружаем адрес указателя → r0
        _varAccessor.LoadAddressToRegister(pointerName, RegType.r1);   // r0 = адрес

        // 4. r0 = адрес + смещение
        _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), RegType.r1.ToUint(), RegType.r3.ToUint()));

        // 5. Сохраняем значение из r1 (которое пришло из StoreR0ToArrayElement)
        _asm.EmitInstruction(InstructionEncoder.EncodeSTORE_IND(RegType.r2.ToUint(), RegType.r1.ToUint(), pointedSize.ToUint()));
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
            _asm.EmitInstruction(InstructionEncoder.EncodeR(
                OpCode.MOV.ToUint(), RegType.r2.ToUint(), RegType.r1.ToUint()));            // r1 = left
            GenerateExpression(binop.Right);                                    // r0 = right
                                                                                // Только CMP обновляет флаги!
            _asm.EmitInstruction(InstructionEncoder.EncodeR(
                OpCode.CMP.ToUint(), RegType.r2.ToUint(), RegType.r1.ToUint()));            // flags = r1 - r0
            GenerateComparisonJump(binop.Operator, trueLabel, falseLabel);
        }
        else
        {
            GenerateExpression(condition);                                      // r0 = значение
                                                                                // Сравнение с нулём: CMP r0, 0 (через r1 как scratch)
            _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(RegType.r2.ToUint()), 0);
            _asm.EmitInstruction(InstructionEncoder.EncodeR(
                OpCode.CMP.ToUint(), RegType.r1.ToUint(), RegType.r2.ToUint()));

            if (trueLabel != null && falseLabel != null)
            {
                _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JNZ.ToUint()), trueLabel);
                _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JMP.ToUint()), falseLabel);
            }
            else if (trueLabel != null)
            {
                _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JNZ.ToUint()), trueLabel);
            }
            else if (falseLabel != null)
            {
                _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JZ.ToUint()), falseLabel);
            }
        }
    }
    private void GenerateComparisonJump(string op, string? trueLabel, string? falseLabel)
    {
        void EmitJmp(string label) => _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JMP.ToUint()), label);
        void EmitCond(OpCode jmpOp, string label) => _asm.EmitJump(InstructionEncoder.EncodeJ(jmpOp.ToUint()), label);

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
            case "==": EmitCond(OpCode.JZ, trueLabel!); EmitJmp(falseLabel!); break;
            case "!=": EmitCond(OpCode.JNZ, trueLabel!); EmitJmp(falseLabel!); break;
            case "<": EmitCond(OpCode.JL, trueLabel!); EmitJmp(falseLabel!); break;
            case ">":
                EmitCond(OpCode.JL, falseLabel!);
                EmitCond(OpCode.JZ, falseLabel!);
                EmitJmp(trueLabel!);
                break;
            case "<=":
                EmitCond(OpCode.JL, trueLabel!);
                EmitCond(OpCode.JZ, trueLabel!);
                EmitJmp(falseLabel!);
                break;
            case ">=":
                EmitCond(OpCode.JL, falseLabel!);
                EmitJmp(trueLabel!);
                break;
        }
    }

    private void GenerateLogicalAndOr(BinaryOpNode binop)
    {
        string endLabel = _getLabel();
        string trueLabel = _getLabel();
        string falseLabel = _getLabel();

        GenerateExpression(binop.Left);
        _asm.EmitInstruction(InstructionEncoder.EncodeR(
        OpCode.CMP.ToUint(), RegType.r1.ToUint(), RegType.r2.ToUint()));
        if (binop.Operator == "&&")
        {
            _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JZ.ToUint()), falseLabel);
        }
        else
        {
            _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JNZ.ToUint()), trueLabel);
        }

        GenerateExpression(binop.Right);
        _asm.EmitInstruction(InstructionEncoder.EncodeR(
        OpCode.CMP.ToUint(), RegType.r1.ToUint(), RegType.r2.ToUint()));
        if (binop.Operator == "&&")
        {
            _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JZ.ToUint()), falseLabel);
        }
        else
        {
            _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JNZ.ToUint()), trueLabel);
        }

        if (binop.Operator == "&&")
        {
            _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(RegType.r1.ToUint()), 1);
        }
        else
        {
            _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(RegType.r1.ToUint()), 0);
        }
        _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JMP.ToUint()), endLabel);

        _asm.MarkLabel(trueLabel);
        _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(RegType.r1.ToUint()), 1);
        _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JMP.ToUint()), endLabel);

        _asm.MarkLabel(falseLabel);
        _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(RegType.r1.ToUint()), 0);

        _asm.MarkLabel(endLabel);
    }


    private void GenerateBinaryOp(BinaryOpNode binop)
    {
        GenerateBinaryOp(binop, RegType.r1);
    }


    private void EmitBinaryOperation(string op, RegType dest, RegType src)
    {
        switch (op)
        {
            case "+": _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), dest.ToUint(), src.ToUint())); break;
            case "-": _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.SUB.ToUint(), dest.ToUint(), src.ToUint())); break;
            case "*": _asm.EmitInstruction(InstructionEncoder.EncodeMULT_INT(dest.ToUint(), src.ToUint())); break;
            case "/": _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.DIV.ToUint(), dest.ToUint(), src.ToUint())); break;
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
            _ => ThrowHelper.ThrowMiniC<long>(ErrorCode.NotSupported, $"Unsupported operator: {op}"),
        };
        return result;
    }

    private static long ComputeDiv(long value1, long value2) => value2 != 0 ? value1 / value2
            : ThrowHelper.ThrowMiniC<long>(ErrorCode.NotSupported, $"Div on zero");

    public void GenerateComparison(string op)
    {
        _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.SUB.ToUint(), RegType.r2.ToUint(), RegType.r1.ToUint()));
        _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.MOV.ToUint(), RegType.r1.ToUint(), RegType.r2.ToUint()));
        _asm.EmitInstruction(InstructionEncoder.EncodeR(
        OpCode.CMP.ToUint(), RegType.r2.ToUint(), RegType.r1.ToUint()));
        string trueLabel = _getLabel();
        string endLabel = _getLabel();
        _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(RegType.r1.ToUint()), 0);

        switch (op)
        {
            case "<":
                _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.MOV.ToUint(), RegType.r2.ToUint(), RegType.rFL.ToUint()));
                _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(RegType.r3.ToUint()), 2);
                _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.AND.ToUint(), RegType.r2.ToUint(), RegType.r3.ToUint()));
                _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JNZ.ToUint()), trueLabel);
                _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JMP.ToUint()), endLabel);
                _asm.MarkLabel(trueLabel);
                _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(RegType.r1.ToUint()), 1);
                _asm.MarkLabel(endLabel);
                break;
            case ">":
                _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.MOV.ToUint(), RegType.r2.ToUint(), RegType.rFL.ToUint()));
                _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(RegType.r3.ToUint()), 3);
                _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.AND.ToUint(), RegType.r2.ToUint(), RegType.r3.ToUint()));
                _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JZ.ToUint()), trueLabel);
                _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JMP.ToUint()), endLabel);
                _asm.MarkLabel(trueLabel);
                _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(RegType.r1.ToUint()), 1);
                _asm.MarkLabel(endLabel);
                break;
            case "<=":
                _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.MOV.ToUint(), RegType.r2.ToUint(), RegType.rFL.ToUint()));
                _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(RegType.r3.ToUint()), 3);
                _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.AND.ToUint(), RegType.r2.ToUint(), RegType.r3.ToUint()));
                _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JNZ.ToUint()), trueLabel);
                _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JMP.ToUint()), endLabel);
                _asm.MarkLabel(trueLabel);
                _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(RegType.r1.ToUint()), 1);
                _asm.MarkLabel(endLabel);
                break;
            case ">=":
                _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.MOV.ToUint(), RegType.r2.ToUint(), RegType.rFL.ToUint()));
                _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(RegType.r3.ToUint()), 2);
                _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.AND.ToUint(), RegType.r2.ToUint(), RegType.r3.ToUint()));
                _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JZ.ToUint()), trueLabel);
                _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JMP.ToUint()), endLabel);
                _asm.MarkLabel(trueLabel);
                _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(RegType.r1.ToUint()), 1);
                _asm.MarkLabel(endLabel);
                break;
            case "==":
                _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.MOV.ToUint(), RegType.r2.ToUint(), RegType.rFL.ToUint()));
                _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(RegType.r3.ToUint()), 1);
                _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.AND.ToUint(), RegType.r2.ToUint(), RegType.r3.ToUint()));
                _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JNZ.ToUint()), trueLabel);
                _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JMP.ToUint()), endLabel);
                _asm.MarkLabel(trueLabel);
                _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(RegType.r1.ToUint()), 1);
                _asm.MarkLabel(endLabel);
                break;
            case "!=":
                _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.MOV.ToUint(), RegType.r2.ToUint(), RegType.rFL.ToUint()));
                _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(RegType.r3.ToUint()), 1);
                _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.AND.ToUint(), RegType.r2.ToUint(), RegType.r3.ToUint()));
                _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JZ.ToUint()), trueLabel);
                _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JMP.ToUint()), endLabel);
                _asm.MarkLabel(trueLabel);
                _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(RegType.r1.ToUint()), 1);
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
                _asm.EmitInstruction(InstructionEncoder.EncodeU(OpCode.NOT.ToUint(), RegType.r1.ToUint()));
                _asm.EmitInstruction(InstructionEncoder.EncodeU(OpCode.INC.ToUint(), RegType.r1.ToUint()));
                break;
            case "!":
                GenerateExpression(unop.Operand);
                string trueLabel = _getLabel();
                string endLabel = _getLabel();
                _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(RegType.r1.ToUint()), 1);
                _asm.EmitInstruction(InstructionEncoder.EncodeR(
                OpCode.CMP.ToUint(), RegType.r1.ToUint(), RegType.r2.ToUint()));
                _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JNZ.ToUint()), trueLabel);
                _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(RegType.r1.ToUint()), 0);
                _asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JMP.ToUint()), endLabel);
                _asm.MarkLabel(trueLabel);
                _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(RegType.r1.ToUint()), 1);
                _asm.MarkLabel(endLabel);
                break;
            case "~":
                GenerateExpression(unop.Operand);
                _asm.EmitInstruction(InstructionEncoder.EncodeU(OpCode.NOT.ToUint(), RegType.r1.ToUint()));
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
                _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(CodeGenUtils.TMP_REG), (ulong)loc.StackOffset);
                _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), CodeGenUtils.TMP_REG, RegType.rSP.ToUint()));
                _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.MOV.ToUint(), RegType.r1.ToUint(), CodeGenUtils.TMP_REG));
            }
            else if (_globalMem.Contains(varName))
            {
                string label = GlobalMemoryManager.GetLabel(varName);
                _asm.EmitInstruction64WithLabel(InstructionEncoder.EncodeLDI(RegType.r1.ToUint()), label);
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
        _asm.EmitInstruction(InstructionEncoder.EncodeLOAD_IND(RegType.r1.ToUint(), RegType.r1.ToUint(), size.ToUint()));
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
            _asm.EmitInstruction64WithLabel(InstructionEncoder.EncodeSTORE(RegType.r1.ToUint(), opSize.ToUint()), label);
        }

        _asm.EmitJump(InstructionEncoder.EncodeCALL(), $"func_{call.Name}");
    }}
