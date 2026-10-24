using Nantuc.Compiler.C;
using Nantuc.Kernel.Common;

namespace Nantuc.Compiler.C.CodeGenerator;

/// <summary>
/// Строит граф интерференции и раскрашивает его жадным алгоритмом.
/// </summary>
public static class RegisterAllocator
{
    private const int TotalRegisters = 32; // r0..rIP

    // Резервируем: r0..r4 — возврат/временные в кодогенерации,
    // rFL/rSP/rHP/rIP — системные.
    private static readonly HashSet<RegType> ReservedRegs =
    [
        RegType.r0, RegType.r1, RegType.r2, RegType.r3, RegType.r4,
        RegType.r27,
        RegType.rFL, RegType.rSP, RegType.rHP, RegType.rIP,
    ];

    private static List<RegType> GetAvailableRegisters()
    {
        var regs = new List<RegType>();
        for (int i = 0; i < TotalRegisters; i++)
        {
            var reg = (RegType)i;
            if (!ReservedRegs.Contains(reg))
                regs.Add(reg);
        }
        return regs;   // r5..r27 — 23 регистра под переменные
    }

    /// <summary>
    /// Выполняет распределение регистров для заданной функции.
    /// </summary>
    /// <param name="func">AST функции</param>
    /// <param name="localVars">Все локальные переменные функции (включая параметры)</param>
    /// <param name="structTable">Таблица структур для определения размера</param>
    /// <returns>Результат раскраски</returns>
    public static RegisterAllocationResult AllocateRegisters(
        FunctionNode func,
        Dictionary<string, VariableNode> localVars,
        Dictionary<string, StructLayout> structTable)
    {
        // 1. Отбираем переменные-кандидаты: скаляры, не массивы и не структуры.
        var candidates = new Dictionary<string, VariableNode>();
        foreach (var kv in localVars)
        {
            var name = kv.Key;
            var varNode = kv.Value;
            // Пропускаем массивы, структуры и указатели на структуры (для простоты)
            if (varNode.IsArray || structTable.ContainsKey(varNode.Type))
                continue;
            candidates[name] = varNode;
        }

        // 2. Получаем доступные регистры (например r4..r23).
        var availableRegs = GetAvailableRegisters();

        // 3. Строим граф интерференции (упрощённо: пересечение диапазонов жизни).
        var interference = BuildInterferenceGraph(func, candidates);

        // 4. Жадная раскраска графа.
        var coloring = GreedyColorGraph(interference, availableRegs);

        // 5. Формируем результат.
        var result = new RegisterAllocationResult();
        foreach (var varName in candidates.Keys)
        {
            if (coloring.TryGetValue(varName, out var reg))
            {
                result.VarToRegister[varName] = reg;
                result.UsedRegisters.Add(reg);
            }
            else
            {
                // Переменная не поместилась в регистр – будет в стеке.
                result.VarToRegister[varName] = null;
            }
        }
        return result;
    }

    // ------------------------------------------------------------
    // Вспомогательные методы
    // ------------------------------------------------------------

    /// <summary>
    /// Строит граф интерференции на основе линейного сканирования AST.
    /// Каждая переменная получает диапазон [firstDef, lastUse].
    /// Если диапазоны пересекаются, добавляется ребро.
    /// </summary>
    private static Dictionary<string, HashSet<string>> BuildInterferenceGraph(
        FunctionNode func,
        Dictionary<string, VariableNode> candidates)
    {
        // Собираем все определения и использования в линейном порядке.
        var (Defs, Uses) = CollectVarEvents(func.Body);

        // Добавляем определения для параметров (в начало функции)
        foreach (var param in func.Parameters)
        {
            if (candidates.ContainsKey(param.Name))
            {
                AddDef(Defs, param.Name, 0);
            }
        }
        // Для каждой переменной-кандидата вычисляем живой диапазон.
        var ranges = new Dictionary<string, (int start, int end)>();
        foreach (var name in candidates.Keys)
        {
            if (Defs.TryGetValue(name, out var defPositions) && defPositions.Count > 0)
            {
                int start = defPositions.Min();
                int end = defPositions.Max(); // конец – последнее использование
                if (Uses.TryGetValue(name, out var usePositions) && usePositions.Count > 0)
                    end = Math.Max(end, usePositions.Max());
                ranges[name] = (start, end);
            }
        }

        // Строим граф: ребро, если диапазоны пересекаются.
        var graph = new Dictionary<string, HashSet<string>>();
        foreach (var name in candidates.Keys)
            graph[name] = [];

        var names = candidates.Keys.ToList();
        for (int i = 0; i < names.Count; i++)
        {
            for (int j = i + 1; j < names.Count; j++)
            {
                var a = names[i];
                var b = names[j];
                if (!ranges.ContainsKey(a) || !ranges.ContainsKey(b))
                    continue;

                var (aStart, aEnd) = ranges[a];
                var (bStart, bEnd) = ranges[b];

                // Пересечение: max(start) <= min(end)
                if (Math.Max(aStart, bStart) <= Math.Min(aEnd, bEnd))
                {
                    graph[a].Add(b);
                    graph[b].Add(a);
                }
            }
        }
        return graph;
    }

    /// <summary>
    /// Собирает позиции определений и использований переменных в линейном порядке обхода AST.
    /// </summary>
    private static (Dictionary<string, List<int>> Defs, Dictionary<string, List<int>> Uses)
CollectVarEvents(BlockNode body)
    {
        var defs = new Dictionary<string, List<int>>();
        var uses = new Dictionary<string, List<int>>();
        int counter = 0;

        void Walk(ASTNode node)
        {
            if (node == null) return;
            counter++;

            switch (node)
            {
                case VariableNode varNode:
                    // Определение переменной (при объявлении)
                    AddDef(defs, varNode.Name, counter);
                    if (varNode.Initializer != null)
                        Walk(varNode.Initializer);
                    break;

                case AssignmentNode assign:
                    // Левая часть – определение
                    if (assign.LValue == null)
                    {
                        AddDef(defs, assign.Name, counter);
                    }
                    // Правая часть – использование
                    Walk(assign.Value);
                    break;

                case IdentifierNode id:
                    AddUse(uses, id.Name, counter);
                    break;

                case BinaryOpNode bin:
                    Walk(bin.Left);
                    Walk(bin.Right);
                    break;

                case UnaryOpNode un:
                    Walk(un.Operand);
                    break;

                case IfNode ifNode:
                    Walk(ifNode.Condition);
                    Walk(ifNode.ThenBlock);
                    if (ifNode.ElseBlock != null)
                        Walk(ifNode.ElseBlock);
                    break;

                case WhileNode whileNode:
                    Walk(whileNode.Condition);
                    Walk(whileNode.Body);
                    break;

                case ForNode forNode:
                    if (forNode.Init != null) Walk(forNode.Init);
                    if (forNode.Condition != null) Walk(forNode.Condition);
                    Walk(forNode.Body);
                    if (forNode.Increment != null) Walk(forNode.Increment);
                    break;

                case ReturnNode ret:
                    if (ret.Value != null) Walk(ret.Value);
                    break;

                case FunctionCallNode call:
                    foreach (var arg in call.Arguments)
                        Walk(arg);
                    break;

                case ArrayAccessNode arr:
                    Walk(arr.Index);
                    AddUse(uses, arr.ArrayName, counter); // использование имени массива (адрес)
                    break;

                case MemberAccessNode member:
                    Walk(member.Object);
                    break;

                case AddressOfNode addrOf:
                    Walk(addrOf.Operand);
                    break;

                case DereferenceNode deref:
                    Walk(deref.Operand);
                    break;

                // Для блоков – рекурсивно обходим все инструкции
                case BlockNode block:
                    foreach (var stmt in block.Statements)
                        Walk(stmt);
                    break;

                default:
                    // Пропускаем неизвестные узлы
                    break;
            }
        }

        Walk(body);
        return (defs, uses);
    }

    private static void AddDef(Dictionary<string, List<int>> dict, string name, int pos)
    {
        if (!dict.TryGetValue(name, out var list))
            dict[name] = list = [];
        list.Add(pos);
    }

    private static void AddUse(Dictionary<string, List<int>> dict, string name, int pos)
    {
        if (!dict.TryGetValue(name, out var list))
            dict[name] = list = [];
        list.Add(pos);
    }

    /// <summary>
    /// Жадная раскраска графа. Возвращает отображение переменная -> регистр (или не содержит, если не раскрашена).
    /// </summary>
    private static Dictionary<string, RegType> GreedyColorGraph(
        Dictionary<string, HashSet<string>> graph,
        List<RegType> availableRegs)
    {
        var coloring = new Dictionary<string, RegType>();
        var usedByNeighbors = new HashSet<RegType>();

        // Сортируем вершины по убыванию степени (для лучшего результата)
        var ordered = graph.Keys
            .OrderByDescending(v => graph[v].Count)
            .ToList();

        foreach (var varName in ordered)
        {
            usedByNeighbors.Clear();
            foreach (var neighbor in graph[varName])
            {
                if (coloring.TryGetValue(neighbor, out var neighborReg))
                    usedByNeighbors.Add(neighborReg);
            }

            // Ищем первый доступный регистр, не занятый соседями.
            RegType? chosen = null;
            foreach (var reg in availableRegs)
            {
                if (!usedByNeighbors.Contains(reg))
                {
                    chosen = reg;
                    break;
                }
            }

            if (chosen.HasValue)
            {
                coloring[varName] = chosen.Value;
            }
            // Если регистра не нашлось, переменная останется в стеке.
        }
        return coloring;
    }
}
