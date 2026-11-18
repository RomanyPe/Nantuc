using System.Globalization;
using System.Numerics;

namespace Nantuc.Diagnostics;

public readonly record struct DiagnosticData
{
    /// <summary>Строковое представление аргумента (никогда не null).</summary>
    public string Text { get; }

    public DiagnosticData(string? text) => Text = text ?? string.Empty;

    public override string ToString() => Text;

    public static DiagnosticData Of(bool value) => new(value ? "true" : "false");

    public static DiagnosticData Of(string? value) => new(value);

    public static DiagnosticData Of<T>(T value) where T : IBinaryNumber<T>
        => new(value.ToString(null, CultureInfo.InvariantCulture));

    public static DiagnosticData Of<T>(T? value) where T : struct, IBinaryNumber<T>
        => new(value?.ToString(null, CultureInfo.InvariantCulture));
}