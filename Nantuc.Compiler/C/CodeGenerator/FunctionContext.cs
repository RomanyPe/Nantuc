using Nantuc.Compiler.C.AST;
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Diagnostics;


namespace Nantuc.Compiler.C.CodeGenerator;

// ============================================================
// FunctionContext – контекст текущей функции
// ============================================================
public class FunctionContext
{
    public Dictionary<string, VarLocation> VarMap { get; private set; } = null!;
    public int TotalLocalSize { get; private set; }
    public string FunctionName { get; private set; } = null!;
    public string EpilogueLabel { get; private set; } = null!;
    public List<RegType> UsedRegisters { get; private set; } = [];
    public int SaveAreaSize { get; private set; } = 0;
    public int ExpressionSaveDepth { get; set; } = 0;


    public static FunctionContext Create(FunctionNode func, Dictionary<string, VariableNode> localVarNodes, Dictionary<string, StructLayout> structTable, ThrowCompiler ThrowCompiler)
    {
        var ctx = new FunctionContext
        {
            FunctionName = func.Name,
            EpilogueLabel = $"__epilogue_{func.Name}"
        };
        var allVars = new List<(string name, string type, bool isArray, int arraySize,
                                bool isPointer, string? pointedType,
                                bool isConst, bool isPointedConst)>();

        foreach (var param in func.Parameters)
            allVars.Add((param.Name, param.Type, false, 0, param.IsPointer, param.PointedType,
                         param.IsConst, param.IsPointedConst));

        foreach (var kv in localVarNodes)
        {
            var v = kv.Value;
            if (!allVars.Any(x => x.name == kv.Key))
                allVars.Add((kv.Key, v.Type, v.IsArray, v.ArraySize,
                             v.IsPointer, v.PointedType, v.IsConst, v.IsPointedConst));
        }        // Создаём словарь всех переменных функции (параметры + локальные)
        var allVarNodes = new Dictionary<string, VariableNode>();

        foreach (var param in func.Parameters)
        {
            allVarNodes[param.Name] = new VariableNode(param.Type, param.Name)
            {
                IsPointer = param.IsPointer,
                PointedType = param.PointedType,
                IsArray = false,
                ArraySize = 0
            };
        }

        foreach (var kv in localVarNodes)
        {
            if (!allVarNodes.ContainsKey(kv.Key))
                allVarNodes[kv.Key] = kv.Value;
        }

        RegisterAllocationResult allocation = RegisterAllocator.AllocateRegisters(func, allVarNodes, structTable);
        var varMap = new Dictionary<string, VarLocation>();
        int stackOffset = 0;

        foreach (var (name, type, isArray, arraySize, isPointer, pointedType, isConst, isPointedConst) in allVars)
        {
            StructLayout? layout = null;
            bool isStruct = !isPointer && structTable.TryGetValue(type, out layout);
            OpCodeSize size = OpCodeSize.S64;
            int varSize;

            if (isArray && isPointer)
            {
                // массив указателей: N * 8 байт; TypeSize = S64 — это размер элемента
                varSize = 8 * arraySize;
                size = OpCodeSize.S64;
            }
            else if (isPointer)
            {
                varSize = 8;
                size = OpCodeSize.S64;
            }
            else if (isStruct)
            {
                varSize = layout!.Size;
            }
            else
            {
                size = CodeGenUtils.GetSizeForType(type, ThrowCompiler);
                varSize = isArray ? CodeGenUtils.GetSizeInBytes(size, ThrowCompiler) * arraySize : 8;
            }

            if (isArray && isStruct)
                varSize = layout!.Size * arraySize;
            varSize = (varSize + 7) & ~7; // выравнивание на 8

            // Может ли переменная находиться в регистре?
            bool canUseRegister = !isArray && !structTable.ContainsKey(type) && !isPointer;

            if (canUseRegister && allocation.VarToRegister.TryGetValue(name, out var reg) && reg.HasValue)
            {
                varMap[name] = new VarLocation
                {
                    IsRegister = true,
                    Register = reg.Value,
                    TypeSize = CodeGenUtils.GetSizeForType(type, ThrowCompiler),
                    IsArray = false,
                    ArraySize = 0,
                    IsPointer = isPointer,
                    PointedType = pointedType,
                    IsConst = isConst,
                    IsPointedConst = isPointedConst,
                };
            }
            else
            {
                varMap[name] = new VarLocation
                {
                    IsRegister = false,
                    StackOffset = stackOffset,
                    TypeSize = size,
                    StructTypeName = structTable.ContainsKey(type) ? type : null,
                    IsArray = isArray,
                    ArraySize = arraySize,
                    IsPointer = isPointer,
                    PointedType = pointedType,
                    IsConst = isConst,
                    IsPointedConst = isPointedConst,
                };
                stackOffset += varSize;
            }
        }

        ctx.VarMap = varMap;
        ctx.TotalLocalSize = stackOffset;   // теперь только для стековых переменных
        ctx.SaveAreaSize = (ComputeMaxBinaryDepth(func.Body) + 1) * 8;
        ctx.UsedRegisters = [.. varMap.Values.Where(v => v.IsRegister).Select(v => v.Register)];
        return ctx;
    }
    private static int ComputeMaxBinaryDepth(ASTNode? node)
    {
        if (node is null) return 0;
        return node switch
        {
            // Binary: left сохранён в save-slot, пока генерируется right → +1
            BinaryOpNode b => 1 + Math.Max(
                                  ComputeMaxBinaryDepth(b.Left),
                                  ComputeMaxBinaryDepth(b.Right)),

            UnaryOpNode u => ComputeMaxBinaryDepth(u.Operand),

            // Assignment:
            // - arr[i] = value: value в r1, save, затем index → +1
            // - *ptr = value / obj.field = value: value в r1, save, затем адрес → +1
            AssignmentNode a => a.IndexExpr != null
                ? Math.Max(ComputeMaxBinaryDepth(a.Value),
                           1 + ComputeMaxBinaryDepth(a.IndexExpr))
                : ComputeMaxBinaryDepth(a.Value)
                  + ((a.LValue is MemberAccessNode or DereferenceNode) ? 1 : 0),

            // Function call: N аргументов = N слотов одновременно
            FunctionCallNode fc => Math.Min(fc.Arguments.Count, 4)
                                   + (fc.Arguments.Count > 0
                                          ? fc.Arguments.Max(ComputeMaxBinaryDepth)
                                          : 0),

            // If: left условия сохраняется на время генерации right → +1
            IfNode i => Math.Max(
                1 + ComputeMaxBinaryDepth(i.Condition),
                Math.Max(ComputeMaxBinaryDepth(i.ThenBlock),
                         i.ElseBlock != null ? ComputeMaxBinaryDepth(i.ElseBlock) : 0)),

            // While: то же для условия
            WhileNode w => Math.Max(
                1 + ComputeMaxBinaryDepth(w.Condition),
                ComputeMaxBinaryDepth(w.Body)),

            // For: condition под +1
            ForNode f => Math.Max(
                ComputeMaxBinaryDepth(f.Init),
                Math.Max(
                    1 + ComputeMaxBinaryDepth(f.Condition),
                    Math.Max(ComputeMaxBinaryDepth(f.Increment),
                             ComputeMaxBinaryDepth(f.Body)))),

            ReturnNode r => ComputeMaxBinaryDepth(r.Value),

            VariableNode v => Math.Max(
                ComputeMaxBinaryDepth(v.Initializer),
                v.ArrayInitializer != null
                    ? v.ArrayInitializer.Max(ComputeMaxBinaryDepth)
                    : 0),

            ArrayAccessNode aa => ComputeMaxBinaryDepth(aa.Index),
            MemberAccessNode ma => ComputeMaxBinaryDepth(ma.Object),
            DereferenceNode d => ComputeMaxBinaryDepth(d.Operand),
            AddressOfNode ao => ComputeMaxBinaryDepth(ao.Operand),

            BlockNode blk => blk.Statements.Count > 0
                                ? blk.Statements.Max(ComputeMaxBinaryDepth)
                                : 0,
            DoWhileNode d => Math.Max(
                ComputeMaxBinaryDepth(d.Body),
                1 + ComputeMaxBinaryDepth(d.Condition)),
            IncrementNode inc => 1 + ComputeMaxBinaryDepth(inc.Target),
            IndirectCallNode ic => Math.Min(ic.Arguments.Count, 4)
                       + 1                                            // callee slot
                       + Math.Max(
                           ic.Arguments.Count > 0
                               ? ic.Arguments.Max(ComputeMaxBinaryDepth)
                               : 0,
                           ComputeMaxBinaryDepth(ic.Callee)),
            _ => 0,
        };
    }
}

