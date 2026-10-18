namespace Kernel.Contracts;

public interface ISimulationResult
{
    TimeSpan Elapsed { get; }
    long Steps { get; }
}
