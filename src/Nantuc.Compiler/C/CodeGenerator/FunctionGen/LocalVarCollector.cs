using Nantuc.Compiler.C.AST;

namespace Nantuc.Compiler.C.CodeGenerator.FunctionGen;

// ============================================================
// Рекурсивный сбор локальных переменных по телу функции.
// Возвращает словарь имя → VariableNode. Порядок вставки не важен:
// FunctionContext.Create сортирует/фильтрует сам.
// ============================================================
internal static class LocalVarCollector
{
    public static Dictionary<string, VariableNode> Collect(BlockNode body)
    {
        var map = new Dictionary<string, VariableNode>();
        Visit(body, map);
        return map;
    }

    private static void Visit(BlockNode block, Dictionary<string, VariableNode> map)
    {
        foreach (var stmt in block.Statements)
        {
            switch (stmt)
            {
                case VariableNode v:
                    map[v.Name] = v;
                    break;
                case BlockNode nested:
                    Visit(nested, map);
                    break;
                case IfNode ifn:
                    Visit(ifn.ThenBlock, map);
                    if (ifn.ElseBlock != null) Visit(ifn.ElseBlock, map);
                    break;
                case WhileNode wh:
                    Visit(wh.Body, map);
                    break;
                case ForNode fr:
                    VisitForInit(fr.Init, map);
                    Visit(fr.Body, map);
                    break;
                case DoWhileNode dw when dw.Body is BlockNode bodyBlock:
                    Visit(bodyBlock, map);
                    break;
            }
        }
    }

    // for-init может быть VariableNode, BlockNode с переменными, или выражением.
    private static void VisitForInit(ASTNode? init, Dictionary<string, VariableNode> map)
    {
        switch (init)
        {
            case VariableNode v:
                map[v.Name] = v;
                break;
            case BlockNode blk:
                foreach (var s in blk.Statements)
                    if (s is VariableNode v) map[v.Name] = v;
                break;
        }
    }
}