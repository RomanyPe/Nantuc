using Nantuc.Common;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.ASM.Parser;

public partial class AssemblerParser
{
    // ============================================================
    // Парсинг одной инструкции и Format* методы по формату операндов.
    // ============================================================
    private void ParseInstruction(string instruction)
    {
        _currentLine = instruction;
        instruction = NormalizeSizeExtension(instruction);

        var tokens = instruction.Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return;

        string mnemonic = tokens[0].ToUpperInvariant();
        (string baseMnemonic, OpCodeSize size) = SplitMnemonicAndSize(mnemonic);

        if (!_opMap.TryGetValue(baseMnemonic, out OpCode opCode))
            _asm.ErrorReporter.Throw(ErrorCode.Asm_InvalidInstruction,
                DiagnosticData.Of(baseMnemonic));

        DispatchInstruction(tokens, baseMnemonic, opCode, size);
    }

    private static (string baseMnemonic, OpCodeSize size) SplitMnemonicAndSize(string mnemonic)
    {
        int dot = mnemonic.IndexOf('.');
        if (dot <= 0) return (mnemonic, OpCodeSize.S64);

        string sizePart = mnemonic[(dot + 1)..];
        if (TryParseSize(sizePart, out var sz))
            return (mnemonic[..dot], sz);

        // Неизвестный суффикс — оставляем как есть; _opMap даст понятную ошибку.
        return (mnemonic, OpCodeSize.S64);
    }

    private void DispatchInstruction(string[] tokens, string baseMnemonic, OpCode opCode, OpCodeSize size)
    {
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

            case "LDI":
                FormatLDI(tokens, baseMnemonic);
                break;

            case "LOAD":
            case "LOAD_UNSAFE":
            case "STORE":
            case "STORE_UNSAFE":
                FormatLOAD_STORE(tokens, size, baseMnemonic, opCode);
                break;

            case "LOAD_IND":
            case "STORE_IND":
            case "LOAD_IND_UNSAFE":
            case "STORE_IND_UNSAFE":
                FormatLOAD_IND_STORE_IND(tokens, size, baseMnemonic, opCode);
                break;

            case "JMP":
            case "JZ":
            case "JNZ":
            case "JG":
            case "JL":
            case "CALL":
                FormatJumpType(tokens, baseMnemonic, opCode);
                break;

            case "CASTZ":
            case "CASTS":
                FormatIWithSize(tokens, baseMnemonic, opCode, size);
                break;

            default:
                _asm.ErrorReporter.Throw(ErrorCode.Asm_UnknownMnemonic, DiagnosticData.Of(baseMnemonic));
                break;
        }
    }

    // ---------- Форматы операндов ----------

    private void FormatR(string[] tokens, string baseMnemonic, OpCode opCode)
    {
        RequireOperands(tokens, 3, baseMnemonic);
        RegType r1 = ParseReg(tokens[1]);
        RegType r2 = ParseReg(tokens[2]);
        _asm.EmitInstR(opCode, r1, r2);
    }

    private void FormatU(string[] tokens, string baseMnemonic, OpCode opCode)
    {
        RequireOperands(tokens, 2, baseMnemonic);
        _asm.EmitInstU(opCode, ParseReg(tokens[1]));
    }

    private void FormatIWithSize(string[] tokens, string baseMnemonic, OpCode opCode, OpCodeSize size)
    {
        RequireOperands(tokens, 2, baseMnemonic);
        _asm.EmitInstI(opCode, ParseReg(tokens[1]), size);
    }

    private void FormatLDI(string[] tokens, string baseMnemonic)
    {
        RequireOperands(tokens, 3, baseMnemonic);
        RegType rLdi = ParseReg(tokens[1]);
        string operand = tokens[2];

        if (IsNumber(operand))
            _asm.EmitLdi(rLdi, ParseNumber(operand));
        else
            _asm.EmitLdi(rLdi, operand);
    }

    private void FormatJumpType(string[] tokens, string baseMnemonic, OpCode opCode)
    {
        RequireOperands(tokens, 2, baseMnemonic);
        string target = tokens[1];

        if (IsNumber(target))
            _asm.EmitInstJAbsolute(opCode, ParseNumber(target));
        else
            _asm.EmitInstJ(opCode, target);
    }

    private void FormatLOAD_STORE(string[] tokens, OpCodeSize size, string baseMnemonic, OpCode opCode)
    {
        RequireOperands(tokens, 3, baseMnemonic);
        RegType rMem = ParseReg(tokens[1]);
        ulong addr = ParseNumber(tokens[2]);
        _asm.EmitInstWithData(opCode, rMem, size, addr);
    }

    private void FormatLOAD_IND_STORE_IND(string[] tokens, OpCodeSize size, string baseMnemonic, OpCode opCode)
    {
        RequireOperands(tokens, 3, baseMnemonic);
        RegType r1 = ParseReg(tokens[1]);
        RegType r2 = ParseReg(tokens[2]);
        _asm.EmitInst(opCode, r1, r2, size);
    }

    private void RequireOperands(string[] tokens, int min, string mnemonic)
    {
        if (tokens.Length < min)
            _asm.ErrorReporter.Throw(ErrorCode.Asm_InvalidInstruction, DiagnosticData.Of(mnemonic));
    }
}
