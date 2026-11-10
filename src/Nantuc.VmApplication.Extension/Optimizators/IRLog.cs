using Nantuc.Compiler.ASM.IR;
using Nantuc.Compiler.C.Optimizators;
using System.Text;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Common;
using Nantuc.Contracts;

namespace Nantuc.VmApplication.Extensions.Optimizators;

public class IRLog(OptimizationId id) : IRogOptimization
{
    private readonly List<OptimizationLogEntry> _entries = [];

    public OptimizationId Id => id;


    public void Append(int passCount, AsmItem item)
    {
        Append(passCount,FormatAsmItem(item));
    }
    
    private static string FormatAsmItem(AsmItem item)
    {
        if (item is AsmLabel label)
            return $"{label.Name}:";

        if (item is AsmInstruction inst)
        {
            var sb = new StringBuilder();
            string opName = inst.OpCode.OpName();
            sb.Append(opName);

            // Суффикс размера для инструкций работы с памятью
            if (inst.OpCode is OpCode.LOAD or OpCode.STORE 
                or OpCode.LOAD_IND or OpCode.STORE_IND 
                or OpCode.STORE_UNSAFE or OpCode.STORE_IND_UNSAFE
                or OpCode.LOAD_UNSAFE or OpCode.LOAD_IND_UNSAFE)
                sb.Append('.').Append(inst.Size.SizeName());

            sb.Append(' ');

            switch (inst.OpCode)
            {
                // Без операндов
                case OpCode.NOP:
                case OpCode.END:
                case OpCode.RET:
                case OpCode.HALT:
                case OpCode.WAKE:
                case OpCode.JMP_IND:
                    return sb.ToString().TrimEnd();

                case OpCode.INC:
                case OpCode.DEC:
                case OpCode.NOT:
                case OpCode.PUSH:
                case OpCode.POP:
                case OpCode.WAKE_INT:
                case OpCode.CALL_IND:
                    return sb.Append(inst.FirstReg).ToString();

                // Два регистра (обычные)
                case OpCode.CMP:
                case OpCode.TEST:
                case OpCode.MOD:
                case OpCode.SHL:
                case OpCode.MOV:
                case OpCode.ADD:
                case OpCode.SUB:
                case OpCode.AND:
                case OpCode.OR:
                case OpCode.XOR:
                case OpCode.IN:
                case OpCode.OUT:
                case OpCode.SHR:
                case OpCode.DIV:
                case OpCode.MULT_INT:
                    return sb.Append(inst.FirstReg).Append(", ").Append(inst.SecondReg).ToString();

                // Два регистра (с размером)
                case OpCode.STORE_IND_UNSAFE:
                case OpCode.LOAD_IND_UNSAFE:
                case OpCode.LOAD_IND:
                case OpCode.STORE_IND:
                    return sb.Append(inst.FirstReg).Append(", ").Append(inst.SecondReg).ToString();

                // LDI: регистр, immediate/метка
                case OpCode.LDI:
                    sb.Append(inst.FirstReg).Append(", ");
                    AppendImmediateOrLabel(sb, inst);
                    return sb.ToString();

                // LOAD/STORE: регистр, адрес (immediate/метка)
                case OpCode.LOAD_UNSAFE:
                case OpCode.STORE_UNSAFE:
                case OpCode.LOAD:
                case OpCode.STORE:
                    sb.Append(inst.FirstReg).Append(", ");
                    AppendImmediateOrLabel(sb, inst);
                    return sb.ToString();

                // Переходы и вызовы
                case OpCode.JMP:
                case OpCode.JZ:
                case OpCode.JNZ:
                case OpCode.JG:
                case OpCode.JL:
                case OpCode.CALL:
                    if (inst.TargetLabel != null)
                        return sb.Append(inst.TargetLabel).ToString();
                    if (inst.Immediate.HasValue)
                        return sb.Append("0x").Append(inst.Immediate.Value.ToString("X")).ToString();
                    return sb.Append("???").ToString();

                default:
                    return "???";
            }
        }
        return "???";
    }

    private static void AppendImmediateOrLabel(StringBuilder sb, AsmInstruction inst)
    {
        if (inst.TargetLabel != null)
            sb.Append(inst.TargetLabel);
        else if (inst.Immediate.HasValue)
            sb.Append("0x").Append(inst.Immediate.Value.ToString("X"));
        else
            sb.Append("???");
    }

    public void Clear() => _entries.Clear();

    public void Append(int pass, string message) =>
        _entries.Add(new OptimizationLogEntry(pass, message));

    public IReadOnlyList<OptimizationLogEntry> GetLogs() => _entries;
}
