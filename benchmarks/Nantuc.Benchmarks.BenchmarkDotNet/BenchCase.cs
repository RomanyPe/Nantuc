namespace Nantuc.Benchmarks.BenchmarkDotNet;

public record BenchCase(
    string Name,
    string ProgramText)
{
    public override string ToString() => Name;
}