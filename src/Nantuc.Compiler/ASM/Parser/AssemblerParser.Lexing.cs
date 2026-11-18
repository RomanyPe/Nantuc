using Nantuc.Diagnostics;

namespace Nantuc.Compiler.ASM.Parser;

public partial class AssemblerParser
{
    // ============================================================
    // Разбиение исходника на строки, обработка комментариев и секций.
    // ============================================================
    private void ProcessLines(string code)
    {
        var lines = code.Split(['\n', ';'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            string line = StripComments(rawLine);
            if (string.IsNullOrEmpty(line)) continue;

            if (line.StartsWith('.'))
            {
                if (ProcessSectionDirective(line)) continue;

                if (_inDataSection && TryProcessDataDirective(line, out string? err))
                {
                    if (err != null)
                        _asm.ErrorReporter.Throw(ErrorCode.Asm_InvalidInstruction, 
                            DiagnosticData.Of(err));
                    continue;
                }
            }

            if (_inDataSection) { ProcessDataLine(line); continue; }

            ProcessCodeLine(line);
        }

        if (_pendingLabel != null)
            _asm.ErrorReporter.Throw(ErrorCode.Asm_InvalidInstruction,
                DiagnosticData.Of($"Label '{_pendingLabel}' has no data"));
    }

    private static string StripComments(string rawLine)
    {
        string line = rawLine.Trim();
        if (string.IsNullOrEmpty(line)) return line;

        int idx = line.IndexOf("//", StringComparison.Ordinal);
        if (idx >= 0) line = line[..idx].Trim();
        return line;
    }

    /// <summary>
    /// Обрабатывает .data / .text / .section. Возвращает true, если строка —
    /// директива секции (и её не надо передавать дальше).
    /// </summary>
    private bool ProcessSectionDirective(string line)
    {
        string? section = GetSectionDirective(line);
        if (section == "data")
        {
            if (!_allowDataSection)
                _asm.ErrorReporter.Throw(ErrorCode.NotSupported,
                    DiagnosticData.Of(".data section is not allowed in this context (inline asm)"));
            _inDataSection = true;
            return true;
        }
        if (section == "text")
        {
            _inDataSection = false;
            return true;
        }
        return false;
    }

    private void ProcessDataLine(string line)
    {
        // "label:" или "label: rest"
        if (line.EndsWith(':'))
        {
            if (_pendingLabel != null)
                _asm.ErrorReporter.Throw(ErrorCode.Asm_InvalidInstruction,
                    DiagnosticData.Of($"Label '{_pendingLabel}' has no data before next label"));
            _pendingLabel = line[..^1].Trim();
            return;
        }

        if (line.Contains(':'))
        {
            var parts = line.Split([':'], 2);
            string label = parts[0].Trim();
            string rest = parts[1].Trim();

            if (_pendingLabel != null)
                _asm.ErrorReporter.Throw(ErrorCode.Asm_InvalidInstruction,
                    DiagnosticData.Of($"Label '{_pendingLabel}' has no data"));
            _pendingLabel = label;

            if (!string.IsNullOrEmpty(rest))
            {
                if (!TryProcessDataDirective(rest, out string? e))
                    _asm.ErrorReporter.Throw(ErrorCode.Asm_InvalidInstruction, 
                        DiagnosticData.Of(rest));
                if (e != null)
                    _asm.ErrorReporter.Throw(ErrorCode.Asm_InvalidInstruction, 
                        DiagnosticData.Of(e));
            }
            return;
        }

        if (TryProcessDataDirective(line, out _)) return;
        _asm.ErrorReporter.Throw(ErrorCode.Asm_InvalidInstruction, 
            DiagnosticData.Of(line));
    }

    private void ProcessCodeLine(string line)
    {
        if (line.EndsWith(':'))
        {
            _asm.MarkLabel(line[..^1].Trim());
            return;
        }

        if (line.Contains(':'))
        {
            var parts = line.Split([':'], 2);
            _asm.MarkLabel(parts[0].Trim());
            string rest = parts[1].Trim();
            if (!string.IsNullOrEmpty(rest)) ParseInstruction(rest);
            return;
        }

        ParseInstruction(line);
    }

    private static string? GetSectionDirective(string line)
    {
        string lower = line.Trim().ToLowerInvariant();
        if (lower is ".data" or ".bss" or ".section .data" or ".section .bss") return "data";
        if (lower is ".text" or ".code" or ".section .text" or ".section .code") return "text";
        return null;
    }

    /// <summary>Нормализует "LOAD . S8" → "LOAD.S8". Аллоцирует только при наличии " . ".</summary>
    private static string NormalizeSizeExtension(string instruction)
    {
        int dot = instruction.IndexOf('.');
        if (dot < 0) return instruction;

        bool hasSpaceBefore = dot > 0 && char.IsWhiteSpace(instruction[dot - 1]);
        bool hasSpaceAfter = dot + 1 < instruction.Length && char.IsWhiteSpace(instruction[dot + 1]);
        if (!hasSpaceBefore && !hasSpaceAfter) return instruction;

        return GetRexExtensionAsm().Replace(instruction, "$1.$2");
    }

    private void FlushPendingData()
    {
        if (_pendingData.Count == 0) return;
        foreach (var item in _pendingData)
            _asm.EmitData(item.Label ?? "", item.Data);
        _pendingData.Clear();
    }
}