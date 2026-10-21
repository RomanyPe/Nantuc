using Nantuc.Compiler.ASM;
using Nantuc.Compiler.ASM.Optimizators;
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
                _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.MOV.ToUint(), targetReg.ToUint(), locReg.ToUint()));
            return;
        }

        if (_funcCtx.VarMap.TryGetValue(varName, out var loc))
        {
            _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(CodeGenUtils.TMP_REG), (ulong)loc.StackOffset);
            _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), CodeGenUtils.TMP_REG, RegType.rSP.ToUint()));
            _asm.EmitInstruction(InstructionEncoder.EncodeLOAD_IND(targetReg.ToUint(), CodeGenUtils.TMP_REG, loc.TypeSize.ToUint()));
            return;
        }

        if (_globalMem.Contains(varName))
        {
            var gInfo = _globalMem.GetInfo(varName)!.Value;
            var size = CodeGenUtils.GetSizeForType(gInfo.Type);
            string label = GlobalMemoryManager.GetLabel(varName);
            _asm.EmitInstruction64WithLabel(InstructionEncoder.EncodeLOAD(targetReg.ToUint(), size.ToUint()), label);
            return;
        }

        ThrowHelper.ThrowMiniC(ErrorCode.CodeGen_UndefinedVariable, varName);
    }

    public void StoreFromRegister(string varName, RegType sourceReg)
    {
        if (TryGetRegister(varName, out var locReg))
        {
            if (locReg != sourceReg)
                _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.MOV.ToUint(), locReg.ToUint(), sourceReg.ToUint()));
            return;
        }

        if (_funcCtx.VarMap.TryGetValue(varName, out var loc))
        {
            _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(CodeGenUtils.TMP_REG), (ulong)loc.StackOffset);
            _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), CodeGenUtils.TMP_REG, RegType.rSP.ToUint()));
            _asm.EmitInstruction(InstructionEncoder.EncodeSTORE_IND(sourceReg.ToUint(), CodeGenUtils.TMP_REG, loc.TypeSize.ToUint()));
            return;
        }

        if (_globalMem.Contains(varName))
        {
            var gInfo = _globalMem.GetInfo(varName)!.Value;
            var size = CodeGenUtils.GetSizeForType(gInfo.Type);
            string label = GlobalMemoryManager.GetLabel(varName);
            _asm.EmitInstruction64WithLabel(InstructionEncoder.EncodeSTORE(sourceReg.ToUint(), size.ToUint()), label);
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
            _asm.EmitInstruction64(InstructionEncoder.EncodeLDI(CodeGenUtils.TMP_REG), (ulong)loc.StackOffset);
            _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), CodeGenUtils.TMP_REG, RegType.rSP.ToUint()));
            if (CodeGenUtils.TMP_REG != targetReg.ToUint())
                _asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.MOV.ToUint(), targetReg.ToUint(), CodeGenUtils.TMP_REG));
            return;
        }

        if (_globalMem.Contains(varName))
        {
            string label = GlobalMemoryManager.GetLabel(varName);
            _asm.EmitInstruction64WithLabel(InstructionEncoder.EncodeLDI(targetReg.ToUint()), label);
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