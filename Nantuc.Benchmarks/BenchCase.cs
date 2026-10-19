namespace Nantuc.Benchmarks;

public record BenchCase(
    string Name,
    string ProgramText,
    int Runs,
    int WarmupRuns,
    long? ExpectedInstructions = null);