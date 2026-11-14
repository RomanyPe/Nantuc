using Nantuc.Common;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C.AST;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.CodeGenerator.Expression;

public partial class ExpressionGenerator
{
    // ============================================================
    // Индексация массивов и указателей: arr[i] / p[i]
    // ============================================================

    // ---------- Чтение ----------
    public void LoadArrayElementToR0(string arrayName, ASTNode indexExpr, RegType targetReg)
    {
        if (_funcCtx.VarMap.TryGetValue(arrayName, out var loc))
        {
            if (loc.IsArray) LoadArrayElementFromLocal(loc, indexExpr, targetReg);
            else if (loc.IsPointer) LoadPointerElementToR0(arrayName, loc, indexExpr, targetReg);
            else _asm.ErrorReporter.Throw(ErrorCode.CodeGen_NonIndexableType, arrayName);
        }
        else if (_globalMem.Contains(arrayName))
        {
            var g = _globalMem.GetInfo(arrayName)!.Value;
            if (g.IsArray) LoadArrayElementFromGlobal(g, arrayName, indexExpr, targetReg);
            else if (g.IsPointer) LoadPointerElementToR0(arrayName, null, indexExpr, targetReg);
            else _asm.ErrorReporter.Throw(ErrorCode.CodeGen_NonIndexableType, arrayName);
        }
        else _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UndefinedVariable, arrayName);
    }

    private void LoadArrayElementFromLocal(VarLocation loc, ASTNode indexExpr, RegType targetReg)
    {
        if (loc.IsArray && loc.StructTypeName != null)
            _asm.ErrorReporter.Throw(ErrorCode.NotSupported,
                "Cannot load struct value from array element (use member access)");

        OpCodeSize typeSize = loc.TypeSize;
        GenerateExpression(indexExpr, targetReg);
        if (typeSize != OpCodeSize.S8)
            _asm.EmitMultiplyByConstant(targetReg, CodeGenUtils.GetSizeInBytes(typeSize, _asm.ErrorReporter));

        _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)loc.StackOffset);
        _asm.EmitAdd(CodeGenUtils.ADDR_SCRATCH_REG_EN, RegType.rSP);
        _asm.EmitAdd(CodeGenUtils.ADDR_SCRATCH_REG_EN, targetReg);
        _asm.EmitLoadInd(targetReg, CodeGenUtils.ADDR_SCRATCH_REG_EN, typeSize);
        _asm.EmitCastToType(targetReg, typeSize, loc.IsSigned);
    }

    private void LoadArrayElementFromGlobal(GlobalInfo gInfo, string arrayName, ASTNode indexExpr, RegType targetReg)
    {
        OpCodeSize typeSize = gInfo.IsPointer ? OpCodeSize.S64
                            : CodeGenUtils.GetSizeForType(gInfo.Type, _asm.ErrorReporter);
        bool signed = !gInfo.IsPointer && gInfo.IsSigned;

        GenerateExpression(indexExpr, targetReg);
        if (typeSize != OpCodeSize.S8)
            _asm.EmitMultiplyByConstant(targetReg, CodeGenUtils.GetSizeInBytes(typeSize, _asm.ErrorReporter));

        _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, GlobalMemoryManager.GetLabel(arrayName));
        _asm.EmitAdd(CodeGenUtils.ADDR_SCRATCH_REG_EN, targetReg);
        _asm.EmitLoadInd(targetReg, CodeGenUtils.ADDR_SCRATCH_REG_EN, typeSize);
        _asm.EmitCastToType(targetReg, typeSize, signed);
    }

    private void LoadPointerElementToR0(string pointerName, VarLocation? loc, ASTNode indexExpr, RegType targetReg)
    {
        OpCodeSize pointedSize;
        bool signed;
        if (loc != null)
        {
            pointedSize = CodeGenUtils.GetSizeForType(loc.PointedType!, _asm.ErrorReporter);
            signed = CodeGenUtils.IsSignedType(loc.PointedType!);
        }
        else if (_globalMem.Contains(pointerName))
        {
            var g = _globalMem.GetInfo(pointerName)!.Value;
            pointedSize = CodeGenUtils.GetSizeForType(g.PointedType!, _asm.ErrorReporter);
            signed = CodeGenUtils.IsSignedType(g.PointedType!);
        }
        else
        {
            pointedSize = CodeGenUtils.GetSizeForType("int", _asm.ErrorReporter);
            signed = false;
        }

        RegType idxReg = (targetReg != RegType.r2) ? RegType.r2 : RegType.r3;
        GenerateExpression(indexExpr, idxReg);
        _varAccessor.LoadToRegister(pointerName, targetReg);
        if (pointedSize != OpCodeSize.S8)
            _asm.EmitMultiplyByConstant(idxReg, CodeGenUtils.GetSizeInBytes(pointedSize, _asm.ErrorReporter));
        _asm.EmitAdd(targetReg, idxReg);
        _asm.EmitLoadInd(targetReg, targetReg, pointedSize);
        _asm.EmitCastToType(targetReg, pointedSize, signed);
    }

    // ---------- Запись ----------
    public void StoreR0ToArrayElement(string arrayName, ASTNode indexExpr)
    {
        int slot = SaveRegToStack(RegType.r0);

        if (_funcCtx.VarMap.TryGetValue(arrayName, out var loc))
        {
            if (loc.IsArray) StoreArrayElementToLocal(loc, indexExpr, slot);
            else if (loc.IsPointer) StorePointerElement(arrayName, loc, indexExpr, slot);
            else _asm.ErrorReporter.Throw(ErrorCode.CodeGen_NonIndexableType, arrayName);
        }
        else if (_globalMem.Contains(arrayName))
        {
            var g = _globalMem.GetInfo(arrayName)!.Value;
            if (g.IsArray) StoreArrayElementToGlobal(g, arrayName, indexExpr, slot);
            else if (g.IsPointer) StorePointerElement(arrayName, null, indexExpr, slot);
            else _asm.ErrorReporter.Throw(ErrorCode.CodeGen_NonIndexableType, arrayName);
        }
        else _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UndefinedVariable, arrayName);

        ReleaseSaveSlot();
    }

    private void StoreArrayElementToLocal(VarLocation loc, ASTNode indexExpr, int slot)
    {
        OpCodeSize typeSize = loc.TypeSize;

        GenerateExpression(indexExpr, RegType.r0);
        if (typeSize != OpCodeSize.S8)
            _asm.EmitMultiplyByConstant(RegType.r0, CodeGenUtils.GetSizeInBytes(typeSize, _asm.ErrorReporter));

        _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)loc.StackOffset);
        _asm.EmitAdd(CodeGenUtils.ADDR_SCRATCH_REG_EN, RegType.rSP);
        _asm.EmitAdd(CodeGenUtils.ADDR_SCRATCH_REG_EN, RegType.r0);

        _asm.EmitLdi(RegType.r3, (ulong)slot);
        _asm.EmitAdd(RegType.r3, RegType.rSP);
        _asm.EmitLoadInd(RegType.r1, RegType.r3, OpCodeSize.S64);

        _asm.EmitStoreInd(RegType.r1, CodeGenUtils.ADDR_SCRATCH_REG_EN, typeSize);
    }

    private void StoreArrayElementToGlobal(GlobalInfo gInfo, string arrayName, ASTNode indexExpr, int slot)
    {
        OpCodeSize typeSize = gInfo.IsPointer ? OpCodeSize.S64
                            : CodeGenUtils.GetSizeForType(gInfo.Type, _asm.ErrorReporter);

        GenerateExpression(indexExpr, RegType.r0);
        if (typeSize != OpCodeSize.S8)
            _asm.EmitMultiplyByConstant(RegType.r0, CodeGenUtils.GetSizeInBytes(typeSize, _asm.ErrorReporter));

        _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, GlobalMemoryManager.GetLabel(arrayName));
        _asm.EmitAdd(CodeGenUtils.ADDR_SCRATCH_REG_EN, RegType.r0);

        _asm.EmitLdi(RegType.r3, (ulong)slot);
        _asm.EmitAdd(RegType.r3, RegType.rSP);
        _asm.EmitLoadInd(RegType.r1, RegType.r3, OpCodeSize.S64);

        _asm.EmitStoreInd(RegType.r1, CodeGenUtils.ADDR_SCRATCH_REG_EN, typeSize);
    }

    private void StorePointerElement(string pointerName, VarLocation? loc, ASTNode indexExpr, int slot)
    {
        OpCodeSize pointedSize;
        if (loc != null)
            pointedSize = CodeGenUtils.GetSizeForType(loc.PointedType!, _asm.ErrorReporter);
        else if (_globalMem.Contains(pointerName))
            pointedSize = CodeGenUtils.GetSizeForType(_globalMem.GetInfo(pointerName)!.Value.PointedType!, _asm.ErrorReporter);
        else
            pointedSize = CodeGenUtils.GetSizeForType("int", _asm.ErrorReporter);

        GenerateExpression(indexExpr, RegType.r2);
        _varAccessor.LoadToRegister(pointerName, RegType.r0);
        if (pointedSize != OpCodeSize.S8)
            _asm.EmitMultiplyByConstant(RegType.r2, CodeGenUtils.GetSizeInBytes(pointedSize, _asm.ErrorReporter));
        _asm.EmitAdd(RegType.r0, RegType.r2);

        _asm.EmitLdi(RegType.r3, (ulong)slot);
        _asm.EmitAdd(RegType.r3, RegType.rSP);
        _asm.EmitLoadInd(RegType.r1, RegType.r3, OpCodeSize.S64);

        _asm.EmitStoreInd(RegType.r1, RegType.r0, pointedSize);
    }

    // ---------- Адрес ----------
    /// <summary>
    /// Вычисляет адрес arr[index] (или p[index] для указателя) и кладёт в addrReg.
    /// addrReg не должен быть r0 — r0 используется как scratch для индекса.
    /// </summary>
    internal void ComputeArrayElementAddress(string arrayName, ASTNode indexExpr, RegType addrReg)
    {
        RegType indexReg = PickIndexReg(addrReg);

        if (_funcCtx.VarMap.TryGetValue(arrayName, out var loc))
        {
            int elemBytes = GetArrayElemBytes(loc);
            GenerateExpression(indexExpr, indexReg);
            if (elemBytes > 1) _asm.EmitMultiplyByConstant(indexReg, elemBytes);
            int slot = SaveRegToStack(indexReg);

            if (loc.IsArray)
            {
                _asm.EmitLdi(addrReg, (ulong)loc.StackOffset);
                _asm.EmitAdd(addrReg, RegType.rSP);
            }
            else _varAccessor.LoadToRegister(arrayName, addrReg);

            RestoreRegFromStack(slot, indexReg);
            _asm.EmitAdd(addrReg, indexReg);
            return;
        }

        if (_globalMem.Contains(arrayName))
        {
            var g = _globalMem.GetInfo(arrayName)!.Value;
            int elemBytes = GetArrayElemBytes(g);
            GenerateExpression(indexExpr, indexReg);
            if (elemBytes > 1) _asm.EmitMultiplyByConstant(indexReg, elemBytes);
            int slot = SaveRegToStack(indexReg);

            if (g.IsArray) _asm.EmitLdi(addrReg, GlobalMemoryManager.GetLabel(arrayName));
            else _varAccessor.LoadToRegister(arrayName, addrReg);

            RestoreRegFromStack(slot, indexReg);
            _asm.EmitAdd(addrReg, indexReg);
            return;
        }

        _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UndefinedVariable, arrayName);
    }

    private static RegType PickIndexReg(RegType addrReg)
    {
        if (addrReg != RegType.r0) return RegType.r0;
        if (addrReg != RegType.r1) return RegType.r1;
        return RegType.r2;
    }

    // ---------- Вспомогательное для массивов ----------
    private OpCodeSize GetIndexedElemSize(string name)
    {
        if (_funcCtx.VarMap.TryGetValue(name, out var loc))
        {
            if (loc.IsArray) return loc.TypeSize;
            if (loc.IsPointer) return CodeGenUtils.GetSizeForType(loc.PointedType!, _asm.ErrorReporter);
        }
        else if (_globalMem.Contains(name))
        {
            var g = _globalMem.GetInfo(name)!.Value;
            if (g.IsArray) return CodeGenUtils.GetSizeForType(g.Type, _asm.ErrorReporter);
            if (g.IsPointer) return CodeGenUtils.GetSizeForType(g.PointedType!, _asm.ErrorReporter);
        }
        _asm.ErrorReporter.Throw(ErrorCode.CodeGen_NonIndexableType, name);
        return default;
    }

    private bool IsArrayElemSigned(string arrayName)
    {
        if (_funcCtx.VarMap.TryGetValue(arrayName, out var loc))
        {
            if (loc.IsArray) return loc.IsSigned;
            if (loc.IsPointer && loc.PointedType != null) return CodeGenUtils.IsSignedType(loc.PointedType);
        }
        else if (_globalMem.Contains(arrayName))
        {
            var g = _globalMem.GetInfo(arrayName)!.Value;
            if (g.IsArray) return !g.IsPointer && CodeGenUtils.IsSignedType(g.Type);
            if (g.IsPointer && g.PointedType != null) return CodeGenUtils.IsSignedType(g.PointedType);
        }
        return false;
    }

    private int GetArrayElemBytes(VarLocation loc)
    {
        if (!loc.IsArray)
        {
            if (loc.PointedType == null) _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UnknownPointedType);
            return (int)GetTypeSizeBytes(loc.PointedType);
        }
        if (loc.StructTypeName != null && _structTable.TryGetValue(loc.StructTypeName, out var l))
            return l.Size;
        return CodeGenUtils.GetSizeInBytes(loc.TypeSize, _asm.ErrorReporter);
    }

    private int GetArrayElemBytes(GlobalInfo gInfo)
    {
        if (!gInfo.IsArray)
        {
            if (gInfo.PointedType == null) _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UnknownPointedType);
            return (int)GetTypeSizeBytes(gInfo.PointedType);
        }
        if (_structTable.TryGetValue(gInfo.Type, out var l)) return l.Size;

        var typeSize = gInfo.IsPointer
            ? OpCodeSize.S64
            : CodeGenUtils.GetSizeForType(gInfo.Type, _asm.ErrorReporter);
        return CodeGenUtils.GetSizeInBytes(typeSize, _asm.ErrorReporter);
    }
}