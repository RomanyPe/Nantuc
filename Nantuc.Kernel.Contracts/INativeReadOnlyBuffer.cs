namespace Nantuc.Kernel.Contracts;

public interface INativeReadOnlyBuffer
{
    nuint Length { get; }
    byte this[nuint index] { get; }
}
