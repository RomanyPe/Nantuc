using Nantuc.Common;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C.AST;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.CodeGenerator.Statement;

public partial class StatementGenerator
{
    // ============================================================
    // Объявления переменных и локальная инициализация массивов.
    // ============================================================

    private void GenerateVariableDeclaration(VariableNode var)
    {
        if (var.Initializer != null)
        {
            GenerateVariableInitializer(var);
            return;
        }
        if (var.ArrayInitializer != null && var.IsArray)
            GenerateLocalArrayInit(var);
    }

    private void GenerateVariableInitializer(VariableNode var)
    {
        if (_funcCtx.VarMap.TryGetValue(var.Name, out var varLoc) && varLoc.IsRegister)
        {
            _exprGen.GenerateExpression(var.Initializer!, varLoc.Register);
            _asm.EmitCastToType(varLoc.Register, varLoc.TypeSize, varLoc.IsSigned);
            return;
        }

        _exprGen.GenerateExpression(var.Initializer!);
        _varAccessor.StoreFromRegister(var.Name, RegType.r0);
    }

    private void GenerateLocalArrayInit(VariableNode var)
    {
        if (!_funcCtx.VarMap.TryGetValue(var.Name, out var loc) || loc.IsRegister)
            _asm.ErrorReporter.Throw(ErrorCode.CodeGen_CannotGetRegisterAddress, var.Name);

        OpCodeSize elemSize = loc.TypeSize;
        int elemBytes = CodeGenUtils.GetSizeInBytes(elemSize, _asm.ErrorReporter);
        int providedCount = Math.Min(var.ArrayInitializer!.Count, var.ArraySize);

        // 1) Явно заданные значения
        for (int i = 0; i < providedCount; i++)
        {
            _exprGen.GenerateExpression(var.ArrayInitializer[i]);
            EmitStoreToLocalArrayElement(loc.StackOffset + i * elemBytes, elemSize);
        }

        // 2) Хвост — нули (C-семантика: int y[10] = {5} ⇒ y[1..9] == 0)
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

    // ---------- Инициализация в for-init ----------
    private void GenerateSingleInit(ASTNode init)
    {
        switch (init)
        {
            case VariableNode varInit:
                GenerateVariableDeclaration(varInit);
                break;
            case AssignmentNode assignInit:
                GenerateAssignment(assignInit);
                break;
            case BlockNode block:
                foreach (var stmt in block.Statements) GenerateSingleInit(stmt);
                break;
            default:
                _exprGen.GenerateExpression(init);
                break;
        }
    }
}