using Nantuc.Compiler.C;

namespace Nantuc.Compiler.C.CodeGenerator;

// ============================================================
// GlobalMemoryManager – управление глобальной памятью
// ============================================================
public class GlobalMemoryManager()
{
    private readonly Dictionary<string, GlobalInfo> _globals = [];

    public void Allocate(string name, string type, bool isArray = false, bool isPointer = false,
                         string? pointedType = null, int arraySize = 0)
    {
        _globals[name] = new GlobalInfo(0, type, isArray, isPointer, pointedType, arraySize);
    }

    public void AllocatePseudoGlobals(FunctionNode func)
    {
        foreach (var param in func.Parameters)
        {
            string globalName = $"__param_{func.Name}_{param.Name}";
            Allocate(globalName, param.Type);
        }
    }

    public void AllocateLocalGlobals(BlockNode block, string funcName)
    {
        foreach (var stmt in block.Statements)
        {
            if (stmt is VariableNode var)
            {
                string globalName = $"__local_{funcName}_{var.Name}";
                Allocate(globalName, var.Type, var.IsArray, var.IsPointer, var.PointedType, var.ArraySize);
            }
            else if (stmt is BlockNode nested)
                AllocateLocalGlobals(nested, funcName);
        }
    }

    public bool TryGetAddress(string name, out ulong address)
    {
        if (_globals.TryGetValue(name, out var info))
        {
            address = info.Address;
            return true;
        }
        address = 0;
        return false;
    }

    public GlobalInfo? GetInfo(string name)
    {
        return _globals.TryGetValue(name, out var info) ? info : null;
    }

    public static string GetLabel(string name) => $"__global_{name}";
    public bool Contains(string name) => _globals.ContainsKey(name);
}