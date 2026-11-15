using Nantuc.Common;
using Nantuc.Compiler.ASM;
using Nantuc.Compiler.ASM.Base;
using Nantuc.Compiler.ASM.IR;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C.CodeGenerator.FunctionCtx;
using Nantuc.Diagnostics;


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
            if (locReg != targetReg) _asm.EmitMov(targetReg, locReg);
            return;   // register уже канонический (см. 5.2)
        }

        if (_funcCtx.VarMap.TryGetValue(varName, out var loc))
        {
            _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)loc.StackOffset);
            _asm.EmitAdd(CodeGenUtils.ADDR_SCRATCH_REG_EN, RegType.rSP);
            _asm.EmitLoadInd(targetReg, CodeGenUtils.ADDR_SCRATCH_REG_EN, loc.TypeSize);
            // NEW: LOAD.S8/S16/S32 zero-extends — для signed нужен CASTS
            _asm.EmitCastToType(targetReg, loc.TypeSize, loc.IsSigned);
            return;
        }

        if (_globalMem.Contains(varName))
        {
            var gInfo = _globalMem.GetInfo(varName)!.Value;
            var size = gInfo.IsPointer ? OpCodeSize.S64
                                       : CodeGenUtils.GetSizeForType(gInfo.Type, _asm.ErrorReporter);
            _asm.EmitLoad(targetReg, size, GlobalMemoryManager.GetLabel(varName));
            // NEW
            _asm.EmitCastToType(targetReg, size, gInfo.IsSigned);
            return;
        }

        _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UndefinedVariable, varName);
    }
    public bool IsConst(string name)
    {
        if (_funcCtx.VarMap != null && _funcCtx.VarMap.TryGetValue(name, out var loc))
            return loc.IsConst;
        if (_globalMem.Contains(name))
            return _globalMem.GetInfo(name)!.Value.IsConst;
        return false;
    }

    public bool IsPointedConst(string name)
    {
        if (_funcCtx.VarMap != null && _funcCtx.VarMap.TryGetValue(name, out var loc))
            return loc.IsPointedConst;
        if (_globalMem.Contains(name))
            return _globalMem.GetInfo(name)!.Value.IsPointedConst;
        return false;
    }

    public void StoreFromRegister(string varName, RegType sourceReg)
    {
        if (TryGetRegister(varName, out var locReg))
        {
            if (locReg != sourceReg) _asm.EmitMov(locReg, sourceReg);
            // NEW: home-регистр должен быть каноническим
            if (_funcCtx.VarMap.TryGetValue(varName, out var loch))
                _asm.EmitCastToType(locReg, loch.TypeSize, loch.IsSigned);
            return;
        }

        if (_funcCtx.VarMap.TryGetValue(varName, out var loc))
        {
            _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)loc.StackOffset);
            _asm.EmitAdd(CodeGenUtils.ADDR_SCRATCH_REG_EN, RegType.rSP);
            _asm.EmitStoreInd(sourceReg, CodeGenUtils.ADDR_SCRATCH_REG_EN, loc.TypeSize);
            return;
        }

        if (_globalMem.Contains(varName))
        {
            var gInfo = _globalMem.GetInfo(varName)!.Value;
            // NEW: указатель всегда 8 байт
            var size = gInfo.IsPointer
                ? OpCodeSize.S64
                : CodeGenUtils.GetSizeForType(gInfo.Type, _asm.ErrorReporter);
            string label = GlobalMemoryManager.GetLabel(varName);
            _asm.EmitStore(sourceReg, size, label);
            return;
        }

        _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UndefinedVariable, varName);
    }
    public void LoadAddressToRegister(string varName, RegType targetReg)
    {
        if (_funcCtx.VarMap.TryGetValue(varName, out var loc))
        {
            if (loc.IsRegister)
                _asm.ErrorReporter.Throw(ErrorCode.CodeGen_CannotGetRegisterAddress, varName);
            _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)loc.StackOffset);
            _asm.EmitAdd(CodeGenUtils.ADDR_SCRATCH_REG_EN, RegType.rSP);
            if (CodeGenUtils.ADDR_SCRATCH_REG_EN != targetReg)
                _asm.EmitMov(targetReg, CodeGenUtils.ADDR_SCRATCH_REG_EN);
            return;
        }

        if (_globalMem.Contains(varName))
        {
            string label = GlobalMemoryManager.GetLabel(varName);
            _asm.EmitLdi(targetReg, label);
            return;
        }

        _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UndefinedVariable, varName);
    }

    public OpCodeSize GetVariableSize(string varName)
    {
        if (_funcCtx.VarMap.TryGetValue(varName, out var loc))
            return loc.TypeSize;
        if (_globalMem.Contains(varName))
        {
            var g = _globalMem.GetInfo(varName)!.Value;
            // NEW: указатель всегда 8 байт
            return g.IsPointer
                ? OpCodeSize.S64
                : CodeGenUtils.GetSizeForType(g.Type, _asm.ErrorReporter);
        }
        return _asm.ErrorReporter.Throw<OpCodeSize>(ErrorCode.CodeGen_UndefinedVariable, varName);
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