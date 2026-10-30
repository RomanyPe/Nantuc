using System.Globalization;

namespace Nantuc.Kernel.Diagnostics;

public readonly record struct DiagnosticData
{
    /// <summary>Строковое представление аргумента (никогда не null).</summary>
    public string Text { get; }

    public DiagnosticData(string? text) => Text = text ?? string.Empty;

    public override string ToString() => Text;

    // ── Фабрики для типов вне неявных конверсий ──────────────────────
    public static DiagnosticData Of(bool value) => new(value ? "true" : "false");
    public static DiagnosticData Of<T>(T? value) => new(value?.ToString());

    // ── Неявные конверсии для частых типов ───────────────────────────
    public static implicit operator DiagnosticData(string? text) => new(text);
    public static implicit operator DiagnosticData(char value) => new(value.ToString());
    public static implicit operator DiagnosticData(int value) => new(value.ToString(CultureInfo.InvariantCulture));
    public static implicit operator DiagnosticData(uint value) => new(value.ToString(CultureInfo.InvariantCulture));
    public static implicit operator DiagnosticData(long value) => new(value.ToString(CultureInfo.InvariantCulture));
    public static implicit operator DiagnosticData(ulong value) => new(value.ToString(CultureInfo.InvariantCulture));

}