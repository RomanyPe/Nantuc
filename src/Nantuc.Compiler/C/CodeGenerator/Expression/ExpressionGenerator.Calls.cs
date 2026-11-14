using Nantuc.Common;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C.AST;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.CodeGenerator.Expression;

public partial class ExpressionGenerator
{
    // ============================================================
    // Вызовы функций: прямые и косвенные (через указатель).
    // Соглашение: args 0..3 -> r0..r3, args 4+ — через pseudo-globals
    // (только для прямых вызовов).
    // ============================================================

    private void GenerateFunctionCall(FunctionCallNode call)
    {
        if (!_functionTable.TryGetValue(call.Name, out var targetFunc))
        {
            // Может, это указатель на функцию?
            if (_funcCtx.VarMap.TryGetValue(call.Name, out _) || _globalMem.Contains(call.Name))
            {
                GenerateIndirectCall(call);
                return;
            }
            _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UnknownFunction, call.Name);
        }

        if (call.Arguments.Count != targetFunc.Parameters.Count)
            _asm.ErrorReporter.Throw(ErrorCode.CodeGen_ArgumentCountMismatch, call.Name);

        // 1) args 4+ -> pseudo-globals
        for (int i = 4; i < call.Arguments.Count; i++)
        {
            GenerateExpression(call.Arguments[i]);
            var param = targetFunc.Parameters[i];
            string globalName = $"__param_{targetFunc.Name}_{param.Name}";
            OpCodeSize opSize = param.IsPointer ? OpCodeSize.S64
                              : CodeGenUtils.GetSizeForType(param.Type, _asm.ErrorReporter);
            _asm.EmitStore(RegType.r0, opSize, GlobalMemoryManager.GetLabel(globalName));
        }

        // 2) args 0..3 — генерируем и сохраняем
        int regCount = Math.Min(call.Arguments.Count, 4);
        var slots = new int[regCount];

        for (int i = 0; i < regCount; i++)
        {
            var param = targetFunc.Parameters[i];
            if (_structTable.ContainsKey(param.Type))
                _asm.ErrorReporter.Throw(ErrorCode.NotSupported,
                    "Struct parameters via registers not yet supported");

            GenerateExpression(call.Arguments[i], (RegType)i);

            if (!param.IsPointer)
            {
                OpCodeSize pSize = CodeGenUtils.GetSizeForType(param.Type, _asm.ErrorReporter);
                _asm.EmitCastToType((RegType)i, pSize, CodeGenUtils.IsSignedType(param.Type));
            }
            slots[i] = SaveRegToStack((RegType)i);
        }

        // 3) восстанавливаем
        for (int i = 0; i < regCount; i++)
            RestoreRegFromStack(slots[i], (RegType)i);

        _asm.EmitCall($"func_{call.Name}");
    }

    /// <summary>
    /// Косвенный вызов, где callee — произвольное выражение (arr[i], p->fp и т.п.).
    /// </summary>
    private void GenerateIndirectCall(IndirectCallNode ic, RegType targetReg)
    {
        if (ic.Arguments.Count > 4)
            _asm.ErrorReporter.Throw(ErrorCode.NotSupported,
                $"Indirect call with >4 args (calling convention limitation): {ic.Callee.NameNode}");

        int regCount = ic.Arguments.Count;
        var argSlots = new int[regCount];

        // 1) Аргументы -> r0..r3, каждый сразу сохраняем на стек
        for (int i = 0; i < regCount; i++)
        {
            GenerateExpression(ic.Arguments[i], (RegType)i);
            argSlots[i] = SaveRegToStack((RegType)i);
        }

        // 2) Callee -> r0 (r0..r3 сейчас свободны — args на стеке)
        GenerateExpression(ic.Callee, RegType.r0);
        int calleeSlot = SaveRegToStack(RegType.r0);

        // 3) LIFO: callee сохранялся последним -> восстанавливаем первым в r4
        RestoreRegFromStack(calleeSlot, RegType.r4);

        // 4) Аргументы обратно в r0..r3
        for (int i = 0; i < regCount; i++)
            RestoreRegFromStack(argSlots[i], (RegType)i);

        // 5) CALL_IND r4
        _asm.EmitInstU(OpCode.CALL_IND, RegType.r4);

        if (targetReg != RegType.r0) _asm.EmitMov(targetReg, RegType.r0);
    }

    /// <summary>
    /// Косвенный вызов через именованную переменную-указатель на функцию.
    /// </summary>
    private void GenerateIndirectCall(FunctionCallNode call)
    {
        if (call.Arguments.Count > 4)
            _asm.ErrorReporter.Throw(ErrorCode.NotSupported,
                $"Indirect call with >4 args (calling convention limitation): {call.Name}");

        int regCount = call.Arguments.Count;
        var slots = new int[regCount];

        for (int i = 0; i < regCount; i++)
        {
            GenerateExpression(call.Arguments[i], (RegType)i);
            slots[i] = SaveRegToStack((RegType)i);
        }

        _varAccessor.LoadToRegister(call.Name, RegType.r4);

        for (int i = 0; i < regCount; i++)
            RestoreRegFromStack(slots[i], (RegType)i);

        _asm.EmitInstU(OpCode.CALL_IND, RegType.r4);
    }
}