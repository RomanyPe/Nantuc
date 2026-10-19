using Nantuc.Compiler.ASM.Optimizators;
using Nantuc.Kernel.Diagnostics;

namespace Nantuc.Compiler.ASM;

/// <summary>
/// Сборщик машинного кода с поддержкой меток и выравнивания по 8 байт для 64-битных данных.
/// </summary>
public class Assembler : AssemblerBase
{
    private readonly MemoryStream _stream;
    private readonly BinaryWriter _writer;
    private readonly Dictionary<string, long> _labels = [];
    private readonly List<(long pos, string label)> _patches = [];

    public Assembler(ulong baseAddress = 0) : base(baseAddress)
    {
        _stream = new MemoryStream();
        _writer = new BinaryWriter(_stream);
    }

    /// <summary>Пометить текущую позицию меткой.</summary>
    public override void MarkLabel(string name)
    {
        if (_labels.TryGetValue(name, out long existingPos))
            ThrowHelper.ThrowMiniC(ErrorCode.Asm_DuplicateLabel,name, existingPos);
        _labels[name] = _stream.Position;
    }

    /// <summary>Записать 32-битную инструкцию (без дополнительных данных).</summary>
    public override void EmitInstruction(uint instruction)
    {
        _writer.Write(instruction);
    }

    /// <summary>Записать инструкцию с последующим 64-битным операндом (выравнивание по 8).</summary>
    public override void EmitInstruction64(uint instruction, ulong data)
    {
        _writer.Write(instruction);
        Align8();
        _writer.Write(data);
    }

    /// <summary>Записать инструкцию перехода с меткой (адрес будет подставлен при сборке).</summary>
    public override void EmitJump(uint jmpOpcode, string label)
    {
        _writer.Write(jmpOpcode);
        Align8();
        long pos = _stream.Position;
        _patches.Add((pos, label));
        _writer.Write(0UL); // placeholder
    }

    /// <summary>Записать инструкцию перехода на абсолютный адрес (без патча).</summary>
    public override void EmitJumpToAbsolute(uint jmpOpcode, ulong absoluteTarget)
    {
        _writer.Write(jmpOpcode);
        Align8();
        _writer.Write(absoluteTarget);
    }

    private void Align8()
    {
        long pos = _stream.Position;
        int pad = (int)(((pos + 7) & ~7) - pos);
        if (pad > 0)
        {
            Span<byte> buffer = stackalloc byte[pad];
            _writer.Write(buffer);
        }
    }

    public override void EmitData(string label, byte[] data)
    {
        if (label != null)
            MarkLabel(label);
        Align8(); // выравнивание до 8
        _writer.Write(data);
    }

    /// <summary>Собрать байт-код, подставить адреса меток.</summary>
    public override byte[] Build()
    {
        byte[] bytes = _stream.ToArray();
        foreach (var (pos, label) in _patches)
        {
            if (!_labels.TryGetValue(label, out long targetPos))
                ThrowHelper.ThrowMiniC(ErrorCode.Asm_UndefinedLabel, label, pos);
            ulong absoluteAddr = _baseAddress + (ulong)targetPos;
            BitConverter.GetBytes(absoluteAddr).CopyTo(bytes, (int)pos);
        }
        _stream.Dispose();
        _writer.Dispose();
        return bytes;
    }

    public override bool HasLabel(string name) => _labels.ContainsKey(name);

    /// <summary>Записать инструкцию с последующим 64-битным операндом-меткой (адрес будет подставлен при сборке).</summary>
    public override void EmitInstruction64WithLabel(uint instruction, string label)
    {
        _writer.Write(instruction);
        Align8();
        long pos = _stream.Position;
        _patches.Add((pos, label));
        _writer.Write(0UL); // placeholder
    }
}
