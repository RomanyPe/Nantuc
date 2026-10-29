using Nantuc.Compiler.ASM.Base;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Diagnostics;
using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace Nantuc.Compiler.ASM;

/// <summary>
/// Парсер ассемблерного текста, преобразующий его в байт-код.
/// Поддерживаемые инструкции:
///   NOP, END, RET,
///   MOV, ADD, SUB, AND, OR, XOR (два регистра),
///   INC, DEC, NOT, PUSH, POP (один регистр),
///   LDI (регистр, константа),
///   LOAD.S8|.S16|.S32|.S64 (регистр, адрес),
///   STORE.S8|.S16|.S32|.S64 (регистр, адрес),
///   JMP, JZ, JNZ, JG, JL, CALL (метка или абсолютный адрес).
/// </summary>
public partial class AssemblerParser
{
    private static readonly Dictionary<string, RegType> _regMapLex = new(StringComparer.OrdinalIgnoreCase)
    {
        { "r0", RegType.r0 },
        { "r1", RegType.r1 },
        { "r2", RegType.r2 },
        { "r3", RegType.r3 },
        { "r4", RegType.r4 },
        { "r5", RegType.r5 },
        { "r6", RegType.r6 },
        { "r7", RegType.r7 },
        { "r8", RegType.r8 },
        { "r9", RegType.r9 },
        { "r10", RegType.r10 },
        { "r11", RegType.r11 },
        { "r12", RegType.r12 },
        { "r13", RegType.r13 },
        { "r14", RegType.r14 },
        { "r15", RegType.r15 },
        { "r16", RegType.r16 },
        { "r17", RegType.r17 },
        { "r18", RegType.r18 },
        { "r19", RegType.r19 },
        { "r20", RegType.r20 },
        { "r21", RegType.r21 },
        { "r22", RegType.r22 },
        { "r23", RegType.r23 },
        { "r24", RegType.r24 },
        { "r25", RegType.r25 },
        { "r26", RegType.r26 },
        { "r27", RegType.r27 },
        { "rFL", RegType.rFL },
        { "rSP", RegType.rSP },
        { "rHP", RegType.rHP },
        { "rIP", RegType.rIP }
    };


    private static readonly Dictionary<string, OpCode> _opMapLex = new(StringComparer.OrdinalIgnoreCase)
    {
        { "NOP", OpCode.NOP },
        { "END", OpCode.END },
        { "RET", OpCode.RET },
        { "MOV", OpCode.MOV },
        { "ADD", OpCode.ADD },
        { "SUB", OpCode.SUB },
        { "AND", OpCode.AND },
        { "OR", OpCode.OR },
        { "XOR", OpCode.XOR },
        { "INC", OpCode.INC },
        { "DEC", OpCode.DEC },
        { "NOT", OpCode.NOT },
        { "PUSH", OpCode.PUSH },
        { "POP", OpCode.POP },
        { "LDI", OpCode.LDI },
        { "LOAD", OpCode.LOAD },
        { "STORE", OpCode.STORE },
        { "JMP", OpCode.JMP },
        { "JZ", OpCode.JZ },
        { "JNZ", OpCode.JNZ },
        { "JG", OpCode.JG },
        { "JL", OpCode.JL },
        { "CALL", OpCode.CALL },
        { "LOAD_IND", OpCode.LOAD_IND },
        { "STORE_IND", OpCode.STORE_IND },
        { "IN", OpCode.IN },
        { "OUT", OpCode.OUT },
        { "CALL_IND", OpCode.CALL_IND },
        { "JMP_IND", OpCode.JMP_IND },
        { "SHR", OpCode.SHR },
        { "SHL", OpCode.SHL },
        { "MULT_INT", OpCode.MULT_INT },
        { "DIV", OpCode.DIV },
        { "MOD", OpCode.MOD },
        { "HALT", OpCode.HALT },
        { "WAKE", OpCode.WAKE},
        { "WAKE_INT", OpCode.WAKE_INT },
        { "LOAD_IND_UNSAFE", OpCode.LOAD_IND_UNSAFE},
        { "LOAD_UNSAFE", OpCode.LOAD_UNSAFE},
        { "STORE_IND_UNSAFE", OpCode.STORE_IND_UNSAFE},
        { "STORE_UNSAFE", OpCode.STORE_UNSAFE},
        { "CMP", OpCode.CMP},
        { "TEST", OpCode.TEST},
    };
    private sealed record PendingData(string? Label, byte[] Data);

    private readonly List<PendingData> _pendingData = [];
    private bool _inDataSection = false;
    private string? _pendingLabel = null;
    private bool _allowDataSection = false;
    private string _currentLine = string.Empty;
    private AssemblerBase _asm = null!;
    private static readonly FrozenDictionary<string, RegType> _regMap = _regMapLex.ToFrozenDictionary();
    private static readonly FrozenDictionary<string, OpCode> _opMap = _opMapLex.ToFrozenDictionary();

    public byte[] Assemble(string code, ulong baseAddress, ThrowCompiler throwCompiler)
    {
        var asm = new Assembler(baseAddress, throwCompiler);
        Assemble(code, asm, allowDataSection: true);
        return asm.Build();
    }

    public byte[] Build(string code, AssemblerBase asm)
    {
        Assemble(code, asm, allowDataSection: true);
        return asm.Build();
    }

    public void Assemble(string code, AssemblerBase asm, bool allowDataSection = false)
    {
        _asm = asm;
        _allowDataSection = allowDataSection;
        _pendingData.Clear();
        _inDataSection = false;
        _pendingLabel = null;

        ProcessLines(code);
        FlushPendingData();
    }

    private void ProcessLines(string code)
    {
        var lines = code.Split(['\n', ';'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // Удаление комментариев
            int commentIdx = line.IndexOf(';');
            if (commentIdx < 0) commentIdx = line.IndexOf("//");
            if (commentIdx >= 0)
                line = line[..commentIdx].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // --- директивы секций ---
            if (line.StartsWith('.'))
            {
                string? section = GetSectionDirective(line);
                if (section == "data")
                {
                    if (!_allowDataSection)
                        _asm.ThrowCompiler.Throw(ErrorCode.NotSupported,
                            ".data section is not allowed in this context (inline asm)");
                    _inDataSection = true;
                    continue;
                }
                if (section == "text")
                {
                    _inDataSection = false;
                    continue;
                }
                string? err = null;
                // Директива данных — только внутри .data
                if (_inDataSection && TryProcessDataDirective(line, out err))
                    continue;
                if (err != null)
                    _asm.ThrowCompiler.Throw(ErrorCode.Asm_InvalidInstruction, err);
            }

            // --- секция данных: label + данные ---
            if (_inDataSection)
            {
                if (line.EndsWith(':'))
                {
                    if (_pendingLabel != null)
                        _asm.ThrowCompiler.Throw(ErrorCode.Asm_InvalidInstruction,
                            $"Label '{_pendingLabel}' has no data before next label");
                    _pendingLabel = line[..^1].Trim();
                    continue;
                }
                if (line.Contains(':'))
                {
                    var parts = line.Split([':'], 2);
                    string label = parts[0].Trim();
                    string rest = parts[1].Trim();
                    if (_pendingLabel != null)
                        _asm.ThrowCompiler.Throw(ErrorCode.Asm_InvalidInstruction,
                            $"Label '{_pendingLabel}' has no data");
                    _pendingLabel = label;
                    if (!string.IsNullOrEmpty(rest))
                    {
                        if (!TryProcessDataDirective(rest, out var e2))
                            _asm.ThrowCompiler.Throw(ErrorCode.Asm_InvalidInstruction, rest);
                        if (e2 != null)
                            _asm.ThrowCompiler.Throw(ErrorCode.Asm_InvalidInstruction, e2);
                    }
                    continue;
                }

                if (TryProcessDataDirective(line, out _))
                    continue;
                _asm.ThrowCompiler.Throw(ErrorCode.Asm_InvalidInstruction, line);
                continue;
            }

            // --- секция кода (существующая логика) ---
            if (line.EndsWith(':'))
            {
                string label = line[..^1].Trim();
                _asm.MarkLabel(label);
                continue;
            }
            if (line.Contains(':'))
            {
                var parts = line.Split([':'], 2);
                _asm.MarkLabel(parts[0].Trim());
                string rest = parts[1].Trim();
                if (!string.IsNullOrEmpty(rest))
                    ParseInstruction(rest);
                continue;
            }

            ParseInstruction(line);
        }

        if (_pendingLabel != null)
            _asm.ThrowCompiler.Throw(ErrorCode.Asm_InvalidInstruction,
                $"Label '{_pendingLabel}' has no data");
    }

    // Стало: нормализуем "LOAD . S8" → "LOAD.S8" без аллокаций
    // (работаем по ReadOnlySpan, аллоцируем только если реально нашли " . ")
    private static string NormalizeSizeExtension(string instruction)
    {
        // Быстрая проверка: если в строке нет ".", делать нечего
        int dot = instruction.IndexOf('.');
        if (dot < 0) return instruction;

        // Проверяем, есть ли пробелы вокруг точки (единственный случай, который чинит regex)
        bool hasSpaceBefore = dot > 0 && char.IsWhiteSpace(instruction[dot - 1]);
        bool hasSpaceAfter = dot + 1 < instruction.Length && char.IsWhiteSpace(instruction[dot + 1]);
        if (!hasSpaceBefore && !hasSpaceAfter) return instruction;

        // Медленный путь — только для строк с " . "
        return GetRexExtensionAsm().Replace(instruction, "$1.$2");
    }

    private void FlushPendingData()
    {
        if (_pendingData.Count == 0) return;
        foreach (var item in _pendingData)
            _asm.EmitData(item.Label ?? "", item.Data);
        _pendingData.Clear();
    }

    private static string? GetSectionDirective(string line)
    {
        string lower = line.Trim().ToLowerInvariant();
        if (lower is ".data" or ".bss" or ".section .data" or ".section .bss")
            return "data";
        if (lower is ".text" or ".code" or ".section .text" or ".section .code")
            return "text";
        return null;
    }

    private bool TryProcessDataDirective(string line, out string? errorMsg)
    {
        errorMsg = null;

        int spaceIdx = line.IndexOfAny([' ', '\t']);
        string directive = spaceIdx < 0 ? line : line[..spaceIdx];
        string args = spaceIdx < 0 ? "" : line[(spaceIdx + 1)..].Trim();
        string dir = directive.ToLowerInvariant();

        switch (dir)
        {
            case ".byte":
            case "db":
                // строка тоже допустима: .byte "abc"
                if (args.Length >= 2 && (args[0] == '"' || args[0] == '\''))
                    return TryAddStringData(args, nullTerminated: false, out errorMsg);
                return TryAddIntegerData(args, 1, out errorMsg);
            case ".word":
            case "dw":
                return TryAddIntegerData(args, 2, out errorMsg);
            case ".dword":
            case "dd":
                return TryAddIntegerData(args, 4, out errorMsg);
            case ".quad":
            case "dq":
                return TryAddIntegerData(args, 8, out errorMsg);
            case ".ascii":
                return TryAddStringData(args, nullTerminated: false, out errorMsg);
            case ".asciz":
            case ".string":
                return TryAddStringData(args, nullTerminated: true, out errorMsg);
            case ".zero":
            case ".space":
                {
                    if (string.IsNullOrWhiteSpace(args))
                    { errorMsg = $"{directive} requires a count"; return true; }
                    if (!long.TryParse(args, out long cnt) || cnt < 0)
                    { errorMsg = $"{directive} count must be a non-negative integer"; return true; }
                    AddPendingData(new byte[cnt]);
                    return true;
                }
        }

        return false; // не директива данных
    }

    private bool TryAddIntegerData(string args, int width, out string? errorMsg)
    {
        errorMsg = null;
        if (string.IsNullOrWhiteSpace(args))
        { errorMsg = "missing data values"; return true; }

        var parts = args.Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var bytes = new byte[parts.Length * width];

        for (int i = 0; i < parts.Length; i++)
        {
            if (!TryParseLong(parts[i], out long value))
            { errorMsg = $"invalid numeric value: {parts[i]}"; return true; }

            for (int b = 0; b < width; b++)
                bytes[i * width + b] = (byte)((ulong)value >> (b * 8));
        }

        AddPendingData(bytes);
        return true;
    }

    private bool TryAddStringData(string args, bool nullTerminated, out string? errorMsg)
    {
        errorMsg = null;
        args = args.Trim();
        if (args.Length < 2 || (args[0] != '"' && args[0] != '\'') || args[^1] != args[0])
        { errorMsg = "string literal expected"; return true; }

        string inner = args[1..^1];
        var bytes = new List<byte>(inner.Length + 1);

        for (int i = 0; i < inner.Length; i++)
        {
            char c = inner[i];
            if (c == '\\' && i + 1 < inner.Length)
            {
                i++;
                bytes.Add(inner[i] switch
                {
                    'n' => (byte)'\n',
                    't' => (byte)'\t',
                    'r' => (byte)'\r',
                    '0' => 0,
                    '\\' => (byte)'\\',
                    '"' => (byte)'"',
                    '\'' => (byte)'\'',
                    _ => (byte)inner[i],
                });
            }
            else
            {
                bytes.Add((byte)c);
            }
        }

        if (nullTerminated) bytes.Add(0);
        AddPendingData([.. bytes]);
        return true;
    }

    private static bool TryParseLong(string s, out long value)
    {
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.TryParse(s[2..], System.Globalization.NumberStyles.HexNumber, null, out value);
        return long.TryParse(s, out value);
    }

    private void AddPendingData(byte[] bytes)
    {
        _pendingData.Add(new PendingData(_pendingLabel, bytes));
        _pendingLabel = null;
    }
    private void ParseInstruction(string instruction)
    {
        _currentLine = instruction;
        instruction = NormalizeSizeExtension(instruction);

        var tokens = instruction.Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return;

        string mnemonic = tokens[0].ToUpperInvariant();

        OpCodeSize size = OpCodeSize.S64;
        string baseMnemonic = mnemonic;

        int dot = mnemonic.IndexOf('.');
        if (dot > 0)
        {
            string sizePart = mnemonic[(dot + 1)..];
            if (TryParseSize(sizePart, out var sz))
            {
                baseMnemonic = mnemonic[..dot];
                size = sz;
            }
            // если TryParseSize вернул false — оставляем как есть,
            // и если это опечатка — вылетит по _opMap с внятным сообщением
        }

        if (!_opMap.TryGetValue(baseMnemonic, out OpCode opCode))
            _asm.ThrowCompiler.Throw(ErrorCode.Asm_InvalidInstruction, baseMnemonic);

        switch (baseMnemonic)
        {
            // Без операндов
            case "NOP": _asm.EmitNop(); break;
            case "END": _asm.EmitEnd(); break;
            case "RET": _asm.EmitRet(); break;
            case "HALT": _asm.EmitHalt(); break;
            case "WAKE": _asm.EmitWake(); break;

            // Формат R (два регистра)
            case "MOV":
            case "MOD":
            case "ADD":
            case "SUB":
            case "AND":
            case "OR":
            case "XOR":
            case "IN":
            case "OUT":
            case "SHR":
            case "SHL":
            case "DIV":
            case "MULT_INT":
            case "CMP":
            case "TEST":
                FormatR(tokens, baseMnemonic, opCode);
                break;

            // Формат U (один регистр)
            case "JMP_IND":
            case "CALL_IND":
            case "WAKE_INT":
            case "INC":
            case "DEC":
            case "NOT":
            case "PUSH":
            case "POP":
                FormatU(tokens, baseMnemonic, opCode);
                break;

            // LDI: регистр, константа
            case "LDI":
                FormatLDI(tokens, baseMnemonic);
                break;

            // LOAD/STORE: регистр, адрес (константа)
            case "LOAD":
            case "LOAD_UNSAFE":
                FormatLOAD__STORE(tokens, size, baseMnemonic, isLoad: true);
                break;

            case "STORE":
            case "STORE_UNSAFE":
                FormatLOAD__STORE(tokens, size, baseMnemonic, isLoad: false);
                break;

            case "LOAD_IND":
            case "STORE_IND":
            case "LOAD_IND_UNSAFE":
            case "STORE_IND_UNSAFE":
                FormateLOAD_IND__STORE_IND(tokens, size, baseMnemonic, opCode);
                break;

            // Переходы и CALL: метка или абсолютный адрес
            case "JMP":
            case "JZ":
            case "JNZ":
            case "JG":
            case "JL":
            case "CALL":
                FormatJumpType(tokens, baseMnemonic, opCode);
                break;

            default:
                _asm.ThrowCompiler.Throw(ErrorCode.Asm_UnknownMnemonic,baseMnemonic);
                break;
        }
    }
    private static bool TryParseSize(string s, out OpCodeSize size)
    {
        switch (s.ToUpperInvariant())
        {
            case "S8": case "8": size = OpCodeSize.S8; return true;
            case "S16": case "16": size = OpCodeSize.S16; return true;
            case "S32": case "32": size = OpCodeSize.S32; return true;
            case "S64": case "64": size = OpCodeSize.S64; return true;
            default: size = default; return false;
        }
    }
    private void FormatJumpType(string[] tokens, string baseMnemonic, OpCode opCode)
    {
        if (tokens.Length < 2)
            _asm.ThrowCompiler.Throw(ErrorCode.Asm_InvalidInstruction, baseMnemonic);
        string target = tokens[1];

        if (IsNumber(target))
            _asm.EmitInstJAbsolute(opCode, ParseNumber(target));
        else
            _asm.EmitInstJ(opCode, target);
    }

    private void FormateLOAD_IND__STORE_IND(string[] tokens, OpCodeSize size, string baseMnemonic, OpCode opCode)
    {
        if (tokens.Length < 3)
            _asm.ThrowCompiler.Throw(ErrorCode.Asm_InvalidInstruction, baseMnemonic);
        RegType rInd1 = ParseReg(tokens[1]);
        RegType rInd2 = ParseReg(tokens[2]);
        _asm.EmitInst(opCode, rInd1, rInd2, size);
    }

    private void FormatLOAD__STORE(string[] tokens, OpCodeSize size, string baseMnemonic, bool isLoad)
    {
        if (tokens.Length < 3)
            _asm.ThrowCompiler.Throw(ErrorCode.Asm_InvalidInstruction, baseMnemonic);

        RegType rMem = ParseReg(tokens[1]);
        ulong addr = ParseNumber(tokens[2]);

        if (isLoad) _asm.EmitLoad(rMem, size, addr);
        else _asm.EmitStore(rMem, size, addr);
    }

    private void FormatLDI(string[] tokens, string baseMnemonic)
    {
        if (tokens.Length < 3)
            _asm.ThrowCompiler.Throw(ErrorCode.Asm_InvalidInstruction, baseMnemonic);
        RegType rLdi = ParseReg(tokens[1]);
        string operand = tokens[2];
        if (IsNumber(operand))
        {
            ulong value = ParseNumber(operand);
            _asm.EmitLdi(rLdi, value);
        }
        else
        {
            _asm.EmitLdi(rLdi, operand);
        }
    }

    private void FormatU(string[] tokens, string baseMnemonic, OpCode opCode)
    {
        if (tokens.Length < 2)
            _asm.ThrowCompiler.Throw(ErrorCode.Asm_InvalidInstruction, baseMnemonic);
        RegType rU = ParseReg(tokens[1]);
        _asm.EmitInstU(opCode, rU);
    }

    private void FormatR(string[] tokens, string baseMnemonic, OpCode opCode)
    {
        if (tokens.Length < 3)
            _asm.ThrowCompiler.Throw(ErrorCode.Asm_InvalidInstruction, baseMnemonic);
        RegType r1 = ParseReg(tokens[1]);
        RegType r2 = ParseReg(tokens[2]);
        _asm.EmitInstR(opCode, r1, r2);
    }

    private RegType ParseReg(string s)
    {
        if (_regMap.TryGetValue(s, out RegType reg))
            return reg;
        return _asm.ThrowCompiler.Throw<RegType>(ErrorCode.Asm_UnknownRegister, _currentLine);
    }

    private static ulong ParseNumber(string s)
    {
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToUInt64(s[2..], 16);
        return ulong.Parse(s);
    }

    private static bool IsNumber(string s)
    {
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return ulong.TryParse(s.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out _);
        return ulong.TryParse(s, out _);
    }

    
    [GeneratedRegex(@"(\w)\s*\.\s*(\w)")]
    private static partial Regex GetRexExtensionAsm();
}

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
            "JMP_IND", "CALL_IND", "SHL", "MOD"
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