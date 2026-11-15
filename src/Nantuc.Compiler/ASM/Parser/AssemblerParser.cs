using Nantuc.Common;
using Nantuc.Compiler.ASM.Base;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Diagnostics;
using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace Nantuc.Compiler.ASM.Parser;

public partial class AssemblerParser
{
    private static readonly FrozenDictionary<string, RegType> _regMap = AsmLanguageDefinition.RegisterMap;
    private static readonly FrozenDictionary<string, OpCode> _opMap = AsmLanguageDefinition.MnemonicMap;

    private sealed record PendingData(string? Label, byte[] Data);

    private readonly List<PendingData> _pendingData = [];
    private bool _inDataSection = false;
    private string? _pendingLabel = null;
    private bool _allowDataSection = false;
    private string _currentLine = string.Empty;
    private AssemblerBase _asm = null!;

    public byte[] Assemble(string code, ulong baseAddress, ErrorReporter errorReporter)
    {
        var asm = new Assembler(baseAddress, errorReporter);
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

    // GeneratedRegex требует partial на методе и на классе — класс уже partial.
    [GeneratedRegex(@"(\w)\s*\.\s*(\w)")]
    private static partial Regex GetRexExtensionAsm();
}