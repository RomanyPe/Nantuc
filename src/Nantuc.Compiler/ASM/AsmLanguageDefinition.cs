using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace Nantuc.Compiler.ASM;

public static class AsmLanguageDefinition
{
    private static readonly Regex _mnemonicsRegex;
    private static readonly Regex regex;

    public static readonly FrozenSet<string> Mnemonics;
    public static readonly FrozenSet<string> Registers;

    static AsmLanguageDefinition()
    {
        Mnemonics = [
            "NOP", "END", "RET", "MOV", "ADD", "SUB", "AND", "OR", "XOR",
            "INC", "DEC", "NOT", "PUSH", "POP",
            "LDI", "LOAD", "STORE",
            "LOAD_UNSAFE", "STORE_UNSAFE", "JMP", "JZ",
            "JNZ", "JG", "JL", "CALL", "LOAD_IND", "STORE_IND","LOAD_IND_UNSAFE", "STORE_IND_UNSAFE",
            "IN", "OUT", "SHR", "MULT_INT", "DIV", "HALT", "WAKE", "WAKE_INT", "CMP", "TEST",
            "JMP_IND", "CALL_IND", "SHL", "MOD","CASTZ", "CASTS"
        ];

        Registers = [
            "r0", "r1", "r2", "r3", "r4", "r5", "r6", "r7", "r8", "r9",
            "r10", "r11", "r12", "r13", "r14", "r15", "r16", "r17", "r18", "r19",
            "r20", "r21", "r22", "r23", "r24", "r25", "r26", "r27",
            "rFL", "rSP", "rHP", "rIP"
        ];

        _mnemonicsRegex = new(GetMnemonicsPattern(), RegexOptions.IgnoreCase);
        regex = new(GetRegistersPattern(), RegexOptions.IgnoreCase);
    }

    private static string GetRegistersPattern() => $@"\b({string.Join("|", Registers)})\b";
    private static string GetMnemonicsPattern() => $@"\b({string.Join("|", Mnemonics)})\b";

    public static Regex GetMnemonicRegex() => _mnemonicsRegex;
    public static Regex GetRegisterRegex() => regex;
}