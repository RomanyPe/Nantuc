using Nantuc.Compiler.ASM;
using Nantuc.Compiler.ASM.Base;
using Nantuc.Compiler.ASM.IR;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Diagnostics;


namespace Nantuc.Compiler.C.CodeGenerator;

public class VariableAccessor(AssemblerBase asm, FunctionContext funcCtx, GlobalMemoryManager globalMem)
{
    private readonly AssemblerBase _asm = asm;
    private readonly FunctionContext _funcCtx = funcCtx;
    private readonly GlobalMemoryManager _globalMem = globalMem;

    public void LoadToRegister(string varName, RegType targetReg)
    {
        if (TryGetRegister(varName, out var locReg))
        {
            if (locReg != targetReg)
                _asm.EmitMov(targetReg, locReg);
            return;
        }

        if (_funcCtx.VarMap.TryGetValue(varName, out var loc))
        {
            _asm.EmitLdi(CodeGenUtils.TMP_REG_EN, (ulong)loc.StackOffset);
            _asm.EmitAdd(CodeGenUtils.TMP_REG_EN, RegType.rSP);
            _asm.EmitLoadInd(targetReg, CodeGenUtils.TMP_REG_EN, loc.TypeSize);
            return;
        }

        if (_globalMem.Contains(varName))
        {
            var gInfo = _globalMem.GetInfo(varName)!.Value;
            var size = CodeGenUtils.GetSizeForType(gInfo.Type);
            string label = GlobalMemoryManager.GetLabel(varName);
            _asm.EmitLoad(targetReg, size, label);
            return;
        }

        ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UndefinedVariable, varName);
    }

    public void StoreFromRegister(string varName, RegType sourceReg)
    {
        if (TryGetRegister(varName, out var locReg))
        {
            if (locReg != sourceReg)
                _asm.EmitMov(locReg, sourceReg);
            return;
        }

        if (_funcCtx.VarMap.TryGetValue(varName, out var loc))
        {
            _asm.EmitLdi(CodeGenUtils.TMP_REG_EN, (ulong)loc.StackOffset);
            _asm.EmitAdd(CodeGenUtils.TMP_REG_EN, RegType.rSP);
            _asm.EmitStoreInd(sourceReg, CodeGenUtils.TMP_REG_EN, loc.TypeSize);
            return;
        }

        if (_globalMem.Contains(varName))
        {
            var gInfo = _globalMem.GetInfo(varName)!.Value;
            var size = CodeGenUtils.GetSizeForType(gInfo.Type);
            string label = GlobalMemoryManager.GetLabel(varName);
            _asm.EmitStore(sourceReg, size, label);
            return;
        }

        ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UndefinedVariable, varName);
    }

    public void LoadAddressToRegister(string varName, RegType targetReg)
    {
        if (_funcCtx.VarMap.TryGetValue(varName, out var loc))
        {
            if (loc.IsRegister)
                ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_CannotGetRegisterAddress, varName);
            _asm.EmitLdi(CodeGenUtils.TMP_REG_EN, (ulong)loc.StackOffset);
            _asm.EmitAdd(CodeGenUtils.TMP_REG_EN, RegType.rSP);
            if (CodeGenUtils.TMP_REG_EN != targetReg)
                _asm.EmitMov(targetReg, CodeGenUtils.TMP_REG_EN);
            return;
        }

        if (_globalMem.Contains(varName))
        {
            string label = GlobalMemoryManager.GetLabel(varName);
            _asm.EmitLdi(targetReg, label);
            return;
        }

        ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UndefinedVariable, varName);
    }

    public OpCodeSize GetVariableSize(string varName)
    {
        if (_funcCtx.VarMap.TryGetValue(varName, out var loc))
            return loc.TypeSize;
        if (_globalMem.Contains(varName))
            return CodeGenUtils.GetSizeForType(_globalMem.GetInfo(varName)!.Value.Type);
        return ThrowHelper.ThrowMiniC<OpCodeSize>(ErrorCode.CodeGen_UndefinedVariable, varName);
    }

    public bool TryGetRegister(string varName, out RegType reg)
    {
        if (_funcCtx.VarMap.TryGetValue(varName, out var loc) && loc.IsRegister)
        {
            reg = loc.Register;
            return true;
        }
        reg = default;
        return false;
    }
}