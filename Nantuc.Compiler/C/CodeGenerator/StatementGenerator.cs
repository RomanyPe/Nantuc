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
                            _varAccessor.StoreFromRegister(var.Name, RegType.r1);
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
            }
        }
    }
    private void GenerateLocalArrayInit(VariableNode var)
    {
        if (!_funcCtx.VarMap.TryGetValue(var.Name, out var loc) || loc.IsRegister)
            ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_CannotGetRegisterAddress, var.Name);

        OpCodeSize elemSize = loc.TypeSize;
        int elemBytes = CodeGenUtils.GetSizeInBytes(elemSize);
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
            _asm.EmitLdi(RegType.r1, 0);
            for (int i = providedCount; i < var.ArraySize; i++)
                EmitStoreToLocalArrayElement(loc.StackOffset + i * elemBytes, elemSize);
        }
    }

    private void EmitStoreToLocalArrayElement(int offset, OpCodeSize elemSize)
    {
        _asm.EmitLdi(CodeGenUtils.TMP_REG_EN, (ulong)offset);
        _asm.EmitAdd(CodeGenUtils.TMP_REG_EN, RegType.rSP);
        _asm.EmitStoreInd(RegType.r1, CodeGenUtils.TMP_REG_EN, elemSize);
    }

    private void GenerateInlineAsm(InlineAsmNode asm)
    {
        var asmParser = new AssemblerParser();
        asmParser.Assemble(asm.AsmCode, _asm);
    }

    private void GenerateAssignment(AssignmentNode assign)
    {
        // Присваивание элементу массива
        if (assign.IndexExpr != null)
        {
            _exprGen.GenerateExpression(assign.Value);
            _exprGen.StoreR0ToArrayElement(assign.Name, assign.IndexExpr);
            return;
        }

        if (assign.LValue != null)
        {
            if (assign.LValue is DereferenceNode deref)
            {
                _exprGen.GenerateExpression(assign.Value);
                _asm.EmitMov(RegType.r2, RegType.r1);
                _exprGen.GenerateExpression(deref.Operand);
                OpCodeSize size = GetPointedSize(deref.Operand);
                _asm.EmitStoreInd(RegType.r2, RegType.r1, size);
                return;
            }
        }

        if (assign.LValue is MemberAccessNode memberAccess)
        {
            // Генерируем значение правой части в r0, затем сохраняем в поле
            _exprGen.GenerateExpression(assign.Value);   // r0 = значение
                                                         // Сохраняем значение во временный регистр r1
            _asm.EmitMov(RegType.r2, RegType.r1);

            // Вычисляем адрес поля
            _exprGen.GenerateMemberAddress(memberAccess); // нужно написать метод, возвращающий адрес в r0
                                                          // Сохраняем
            OpCodeSize size = GetFieldSize(memberAccess);
            _asm.EmitStoreInd(RegType.r2, RegType.r1, size);
            return;
        }

        // Оптимизация: x = число или x = y, когда x в регистре
        if (assign.IndexExpr == null && assign.LValue == null &&
            _funcCtx.VarMap.TryGetValue(assign.Name, out var regLoc) && regLoc.IsRegister &&
            (assign.Value is NumberNode || assign.Value is IdentifierNode))
        {
            _exprGen.GenerateExpression(assign.Value, regLoc.Register);
            return;
        }

        if (_funcCtx.VarMap.TryGetValue(assign.Name, out var loc) && loc.IsRegister)
        {
            if (assign.Value is BinaryOpNode binop && binop.Left is IdentifierNode id && id.Name == assign.Name
                && (binop.Operator == "+" || binop.Operator == "-" || binop.Operator == "*" || binop.Operator == "/"))
            {
                // x = x op expr
                RegType xReg = loc.Register;
                RegType tempReg = (xReg != RegType.r1) ? RegType.r1 : RegType.r2;
                _exprGen.GenerateExpression(binop.Right, tempReg);
                switch (binop.Operator)
                {
                    case "+": _asm.EmitAdd(xReg, tempReg); break;
                    case "-": _asm.EmitSub( xReg, tempReg); break;
                    case "*": _asm.EmitMultInt(xReg, tempReg); break;
                    case "/": _asm.EmitDiv(xReg, tempReg); break;
                }
                return;
            }

            // Простое присваивание: генерируем значение прямо в регистр переменной
            _exprGen.GenerateExpression(assign.Value, loc.Register);
            return;
        }

        _exprGen.GenerateExpression(assign.Value);
        _varAccessor.StoreFromRegister(assign.Name, RegType.r1);
    }


    private OpCodeSize GetFieldSize(MemberAccessNode node)
    {
        var (_, field, _) = ResolveFieldInfo(node);
        return CodeGenUtils.GetSizeForType(field.Type);
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
        if (rootObject is IdentifierNode id)
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
        }
        else if (rootObject is ArrayAccessNode arrAcc)
        {
            rootStructType = _exprGen.ResolveArrayStructType(arrAcc.ArrayName);
        }

        if (rootStructType == null)
            ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UnknownStructType, "Cannot determine struct type for member access");

        // 3. Проходим по цепочке, получая раскладку и поле на каждом уровне
        StructLayout? currentLayout = null;
        FieldInfo? currentField = null;
        string currentType = rootStructType;

        foreach (var fieldName in chain)
        {
            if (!_structTable.TryGetValue(currentType, out var layout))
                ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UnknownStructType, currentType);
            currentLayout = layout;
            currentField = layout.GetField(fieldName)
                           ?? ThrowHelper.ThrowMiniC<FieldInfo>(ErrorCode.CodeGen_UnknownField, fieldName, currentType);
            currentType = currentField.Type;
        }

        return (currentLayout!, currentField!, currentType);
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
        return ThrowHelper.ThrowMiniC<OpCodeSize>(ErrorCode.CodeGen_UnknownPointedType);
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

        _asm.MarkLabel(startLabel);
        _exprGen.GenerateCondition(whileNode.Condition, null, endLabel);
        GenerateBlock(whileNode.Body);
        _asm.EmitJmp(startLabel);
        _asm.MarkLabel(endLabel);
    }

    private void GenerateFor(ForNode forNode)
    {
        if (forNode.Init != null)
        {
            if (forNode.Init is VariableNode varInit)
            {
                if (varInit.Initializer != null)
                {
                    if (_funcCtx.VarMap.TryGetValue(varInit.Name, out var varLoc) && varLoc.IsRegister)
                        _exprGen.GenerateExpression(varInit.Initializer, varLoc.Register);
                    else
                    {
                        _exprGen.GenerateExpression(varInit.Initializer);
                        _varAccessor.StoreFromRegister(varInit.Name, RegType.r1);
                    }
                }
            }
            else if (forNode.Init is AssignmentNode assignInit)
            {
                GenerateAssignment(assignInit);
            }
            else
            {
                _exprGen.GenerateExpression(forNode.Init);
            }
        }
        string startLabel = _getLabel();
        string endLabel = _getLabel();

        _asm.MarkLabel(startLabel);
        if (forNode.Condition != null)
            _exprGen.GenerateCondition(forNode.Condition, null, endLabel);
        GenerateBlock(forNode.Body);
        if (forNode.Increment != null)
        {
            if (forNode.Increment is AssignmentNode assignIncr)
                GenerateAssignment(assignIncr);
            else
                _exprGen.GenerateExpression(forNode.Increment);
        }
        _asm.EmitJmp(startLabel);
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
}
