using Nantuc.Common;
using Nantuc.Compiler.C.AST;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.CodeGenerator.Expression;

public partial class ExpressionGenerator
{
    // ============================================================
    // Приведение типов: (byte)300, (short)x, (int*)p, (void)expr    
    // ============================================================
    private void GenerateCast(CastNode cast, RegType targetReg)
    {        
        GenerateExpression(cast.Operand, targetReg);
        if (cast.IsPointer || cast.TypeName == "void") return;
        // адрес уже 64-битный; (void) — вычислить и выбросить

        if (_structTable.ContainsKey(cast.TypeName))
        {
            _asm.ErrorReporter.Throw(ErrorCode.NotSupported, $"cast to struct type {cast.TypeName}");
            return;
        }
        var size = CodeGenUtils.GetSizeForType(cast.TypeName, _asm.ErrorReporter);
        _asm.EmitCastToType(targetReg, size, CodeGenUtils.IsSignedType(cast.TypeName));
    }
}
