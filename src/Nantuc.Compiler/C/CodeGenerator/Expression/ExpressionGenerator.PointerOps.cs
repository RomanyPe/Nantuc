using Nantuc.Common;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C.AST;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.CodeGenerator.Expression;

public partial class ExpressionGenerator
{
    // ============================================================
    // Адрес-оператор (&) и разыменование (*), а также метаданные
    // о pointed-типах.
    // ============================================================
    private void GenerateAddressOf(AddressOfNode addrOf, RegType targetReg)
    {
        if (addrOf.Operand is IdentifierNode fid && _functionTable.ContainsKey(fid.Name))
        {
            _asm.EmitLdi(targetReg, $"func_{fid.Name}");
            return;
        }

        if (addrOf.Operand is IdentifierNode id)
        {
            string varName = id.Name;
            if (_funcCtx.VarMap.TryGetValue(varName, out var loc))
            {
                if (loc.IsRegister)
                    _asm.ErrorReporter.Throw(ErrorCode.CodeGen_CannotGetRegisterAddress, varName);
                _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)loc.StackOffset);
                _asm.EmitAdd(CodeGenUtils.ADDR_SCRATCH_REG_EN, RegType.rSP);
                _asm.EmitMov(targetReg, CodeGenUtils.ADDR_SCRATCH_REG_EN);
                return;
            }
            if (_globalMem.Contains(varName))
            {
                _asm.EmitLdi(targetReg, GlobalMemoryManager.GetLabel(varName));
                return;
            }
            _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UndefinedVariable, varName);
            return;
        }
        _asm.ErrorReporter.Throw(ErrorCode.NotSupported,
            "Address-of only supports simple variables currently");
    }

    private void GenerateDereference(DereferenceNode deref, RegType targetReg)
    {
        GenerateExpression(deref.Operand, targetReg);
        OpCodeSize size = GetPointedSize(deref.Operand);
        _asm.EmitLoadInd(targetReg, targetReg, size);
        _asm.EmitCastToType(targetReg, size, IsPointedSigned(deref.Operand));
    }

    private OpCodeSize GetPointedSize(ASTNode expr)
    {
        if (expr is IdentifierNode id)
        {
            if (_funcCtx.VarMap.TryGetValue(id.Name, out var loc) && loc.IsPointer)
                return CodeGenUtils.GetSizeForType(loc.PointedType!, _asm.ErrorReporter);
            if (_globalMem.Contains(id.Name))
            {
                var g = _globalMem.GetInfo(id.Name)!.Value;
                if (g.IsPointer) return CodeGenUtils.GetSizeForType(g.PointedType!, _asm.ErrorReporter);
            }
        }
        return CodeGenUtils.GetSizeForType(
            $"Cannot determine pointed type for dereference of '{expr}'", _asm.ErrorReporter);
    }

    private bool IsPointedSigned(ASTNode operand)
    {
        if (operand is IdentifierNode id)
        {
            if (_funcCtx.VarMap.TryGetValue(id.Name, out var loc) && loc.IsPointer)
                return loc.PointedType != null && CodeGenUtils.IsSignedType(loc.PointedType);
            if (_globalMem.Contains(id.Name))
            {
                var g = _globalMem.GetInfo(id.Name)!.Value;
                if (g.IsPointer && g.PointedType != null)
                    return CodeGenUtils.IsSignedType(g.PointedType);
            }
        }
        return false;
    }
}