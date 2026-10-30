using Nantuc.Compiler.C.AST;
using Nantuc.Compiler.C.CodeGenerator;
using Nantuc.Kernel.Diagnostics;

namespace Nantuc.Compiler.C;

public class StructLayout
{
    public string Name { get; }
    public int Size { get; }
    public IReadOnlyList<FieldInfo> Fields => _fields;
    private readonly List<FieldInfo> _fields = [];

    public StructLayout(string name, List<(string type, string fieldName)> fieldDecls,
                        Dictionary<string, StructLayout> structTable, ThrowCompiler ThrowCompiler)
    {
        Name = name;
        int offset = 0;
        foreach (var (type, fieldName) in fieldDecls)
        {
            int align = CodeGenUtils.GetAlignment(type, structTable);
            int size = CodeGenUtils.GetTypeSize(type, structTable, ThrowCompiler);
            offset = (offset + align - 1) & ~(align - 1);
            _fields.Add(new FieldInfo(fieldName, type, offset, size));
            offset += size;
        }
        Size = (offset + 7) & ~7;
    }

    public static Dictionary<string, StructLayout> Resolve(List<StructDeclNode> decls, ThrowCompiler ThrowCompiler)
    {
        var layouts = new Dictionary<string, StructLayout>();
        var remaining = new Queue<StructDeclNode>(decls);
        int lastResolved;
        do
        {
            lastResolved = 0;
            int count = remaining.Count;
            for (int i = 0; i < count; i++)
            {
                var decl = remaining.Dequeue();
                // Проверяем, все ли вложенные структурные типы уже разрешены
                bool canResolve = true;
                foreach (var (type, _) in decl.Fields)
                {
                    if (!CodeGenUtils.IsPrimitiveType(type) && !layouts.ContainsKey(type))
                    {
                        canResolve = false;
                        break;
                    }
                }
                if (canResolve)
                {
                    layouts[decl.Name] = new StructLayout(decl.Name, decl.Fields, layouts, ThrowCompiler);
                    lastResolved++;
                }
                else
                {
                    remaining.Enqueue(decl);
                }
            }
        } while (remaining.Count > 0 && lastResolved > 0);

        if (remaining.Count > 0)
            ThrowCompiler.Throw(ErrorCode.Parser_StructNotDefined,string.Join(", ", remaining.Select(d => d.Name)));
        return layouts;
    }

    public FieldInfo? GetField(string name) => _fields.Find(f => f.Name == name);
}

public record FieldInfo(string Name, string Type, int Offset, int Size);