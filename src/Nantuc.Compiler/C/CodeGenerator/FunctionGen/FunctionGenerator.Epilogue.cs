using Nantuc.Common;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C.AST;
using Nantuc.Compiler.C.CodeGenerator.FunctionCtx;

namespace Nantuc.Compiler.C.CodeGenerator.FunctionGen;

public partial class FunctionGenerator
{
    // ============================================================
    // Эпилог функции: восстановление стека, callee-saved регистров, ret.
    // Для main — просто END.
    // ============================================================
    private void EmitEpilogue(FunctionContext funcCtx, FunctionNode func)
    {
        if (func.Name == "main")
        {
            _asm.EmitEnd();
            return;
        }

        _asm.MarkLabel(funcCtx.EpilogueLabel);

        int stackAlloc = funcCtx.TotalLocalSize + funcCtx.SaveAreaSize;
        var savedRegs = funcCtx.UsedRegisters.OrderBy(r => (int)r).ToList();

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