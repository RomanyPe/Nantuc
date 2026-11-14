using Nantuc.Common;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C.AST;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.CodeGenerator.Statement;

public partial class StatementGenerator
{
    // ============================================================
    // Управляющие конструкции: if / while / for / do-while /
    // return / break / continue.
    // ============================================================
    private void GenerateIf(IfNode ifNode)
    {
        string elseLabel = _getLabel();
        string endLabel = ifNode.ElseBlock != null ? _getLabel() : elseLabel;

        _exprGen.GenerateCondition(ifNode.Condition, null, elseLabel);
        GenerateBlock(ifNode.ThenBlock);

        if (ifNode.ElseBlock != null)
        {
            _asm.EmitJmp(endLabel);
            _asm.MarkLabel(elseLabel);
            GenerateBlock(ifNode.ElseBlock);
            _asm.MarkLabel(endLabel);
        }
        else
        {
            _asm.MarkLabel(elseLabel);
        }
    }

    private void GenerateWhile(WhileNode whileNode)
    {
        string startLabel = _getLabel();
        string endLabel = _getLabel();

        _loopStack.Push((endLabel, startLabel));   // continue → startLabel
        _asm.MarkLabel(startLabel);
        _exprGen.GenerateCondition(whileNode.Condition, null, endLabel);
        GenerateBlock(whileNode.Body);
        _asm.EmitJmp(startLabel);
        _asm.MarkLabel(endLabel);
        _loopStack.Pop();
    }

    private void GenerateFor(ForNode forNode)
    {
        if (forNode.Init != null) GenerateSingleInit(forNode.Init);

        string startLabel = _getLabel();
        string continueLabel = _getLabel();
        string endLabel = _getLabel();

        _asm.MarkLabel(startLabel);
        if (forNode.Condition != null)
            _exprGen.GenerateCondition(forNode.Condition, null, endLabel);

        _loopStack.Push((endLabel, continueLabel));

        GenerateBlock(forNode.Body);
        _asm.MarkLabel(continueLabel);             // continue → increment

        if (forNode.Increment != null)
        {
            if (forNode.Increment is AssignmentNode assignIncr) GenerateAssignment(assignIncr);
            else _exprGen.GenerateExpression(forNode.Increment);
        }

        _asm.EmitJmp(startLabel);
        _asm.MarkLabel(endLabel);
        _loopStack.Pop();
    }

    private void GenerateDoWhile(DoWhileNode node)
    {
        string startLabel = _getLabel();
        string continueLabel = _getLabel();
        string endLabel = _getLabel();

        _asm.MarkLabel(startLabel);
        _loopStack.Push((endLabel, continueLabel));
        GenerateBlock((BlockNode)node.Body);
        _loopStack.Pop();

        _asm.MarkLabel(continueLabel);
        _exprGen.GenerateCondition(node.Condition, startLabel, endLabel);
        _asm.MarkLabel(endLabel);
    }

    private void GenerateReturn(ReturnNode ret)
    {
        if (ret.Value != null)
        {
            _exprGen.GenerateExpression(ret.Value);
            if (!_funcCtx.ReturnIsVoid && _funcCtx.ReturnSize != OpCodeSize.S64)
                _asm.EmitCastToType(RegType.r0, _funcCtx.ReturnSize, _funcCtx.ReturnIsSigned);
        }
        if (_funcCtx.FunctionName == "main") _asm.EmitEnd();
        else _asm.EmitJmp(_funcCtx.EpilogueLabel);
    }

    private void GenerateBreak()
    {
        if (_loopStack.Count == 0)
            _asm.ErrorReporter.Throw(ErrorCode.CodeGen_BreakOutsideLoop);
        _asm.EmitJmp(_loopStack.Peek().BreakLabel);
    }

    private void GenerateContinue()
    {
        if (_loopStack.Count == 0)
            _asm.ErrorReporter.Throw(ErrorCode.CodeGen_ContinueOutsideLoop);
        _asm.EmitJmp(_loopStack.Peek().ContinueLabel);
    }
}