using Nantuc.Common;
using Nantuc.Compiler.ASM.Utils;

namespace Nantuc.Compiler.C.CodeGenerator.Expression;

public partial class ExpressionGenerator
{
    // ============================================================
    // Общие утилиты: сохранение/восстановление регистров на стеке,
    // размеры типов.
    // ============================================================

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

    private long GetTypeSizeBytes(string type)
    {
        if (_structTable.TryGetValue(type, out var layout)) return layout.Size;
        return CodeGenUtils.GetSizeInBytes(
            CodeGenUtils.GetSizeForType(type, _asm.ErrorReporter), _asm.ErrorReporter);
    }
}