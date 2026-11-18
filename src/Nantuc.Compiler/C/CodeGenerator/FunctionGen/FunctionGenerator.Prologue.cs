using Nantuc.Common;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C.AST;
using Nantuc.Compiler.C.CodeGenerator.FunctionCtx;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.CodeGenerator.FunctionGen;

public partial class FunctionGenerator
{
    // ============================================================
    // Пролог функции: сохранение регистров, выделение стека,
    // маршалинг входящих параметров.
    // ============================================================
    private void EmitPrologue(FunctionContext funcCtx, FunctionNode func)
    {
        bool isMain = func.Name == "main";
        int stackAlloc = funcCtx.TotalLocalSize + funcCtx.SaveAreaSize;
        var savedRegs = funcCtx.UsedRegisters.OrderBy(r => (int)r).ToList();

        // 1) Сохраняем callee-saved регистры (кроме main)
        if (!isMain)
            foreach (var reg in savedRegs)
                _asm.EmitPush(reg);

        // 2) Выделяем стек
        if (stackAlloc > 0)
        {
            _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)stackAlloc);
            _asm.EmitSub(RegType.rSP, CodeGenUtils.ADDR_SCRATCH_REG_EN);
        }

        // 3) Маршалинг параметров (кроме main)
        if (!isMain)
            EmitParameterMarshalling(funcCtx, func);
    }

    private void EmitParameterMarshalling(FunctionContext funcCtx, FunctionNode func)
    {
        for (int i = 0; i < func.Parameters.Count; i++)
        {
            var param = func.Parameters[i];

            if (!funcCtx.VarMap.TryGetValue(param.Name, out var loc))
                _erRep.Throw(ErrorCode.CodeGen_ParameterNotInRegister, DiagnosticData.Of(param.Name));

            if (i < 4)
                EmitRegisterParam(i, loc);
            else
                EmitStackParam(param, loc, func.Name);
        }
    }

    // Параметры 0..3 приходят в r0..r3.
    private void EmitRegisterParam(int index, VarLocation loc)
    {
        RegType incomingReg = (RegType)index;

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

    // Параметры 4+ приходят через pseudo-globals __param_<func>_<name>.
    private void EmitStackParam(ParameterNode param, VarLocation loc, string funcName)
    {
        if (!loc.IsRegister)
            _erRep.Throw(ErrorCode.CodeGen_ParameterNotInRegister, DiagnosticData.Of(param.Name));

        string label = GlobalMemoryManager.GetLabel($"__param_{funcName}_{param.Name}");
        OpCodeSize size = param.IsPointer
            ? OpCodeSize.S64
            : CodeGenUtils.GetSizeForType(param.Type, _erRep);

        _asm.EmitLoad(loc.Register, size, label);

        if (!param.IsPointer)
            _asm.EmitCastToType(loc.Register, size, CodeGenUtils.IsSignedType(param.Type));
    }

    // код улучшения регистрового аллокатора
    /*
    private void EmitStackParam(ParameterNode param, VarLocation loc, string funcName)
    {
        string label = GlobalMemoryManager.GetLabel($"__param_{funcName}_{param.Name}");
        OpCodeSize size = param.IsPointer
            ? OpCodeSize.S64
            : CodeGenUtils.GetSizeForType(param.Type, _erRep);

        _asm.EmitLoad(RegType.r0, size, label);
        if (!param.IsPointer)
            _asm.EmitCastToType(RegType.r0, size, CodeGenUtils.IsSignedType(param.Type));

        if (loc.IsRegister)
        {
            if (loc.Register != RegType.r0) _asm.EmitMov(loc.Register, RegType.r0);
        }
        else
        {
            _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)loc.StackOffset);
            _asm.EmitAdd(CodeGenUtils.ADDR_SCRATCH_REG_EN, RegType.rSP);
            _asm.EmitStoreInd(RegType.r0, CodeGenUtils.ADDR_SCRATCH_REG_EN, loc.TypeSize);
        }
    }

     */
}