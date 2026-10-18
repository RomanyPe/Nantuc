using Kernel.Contracts;

namespace Compiller.C.Optimizators;

public interface ILogOptimization : IReadOnlyLogOptimization
{
    void Clear();
    void Append(int pass, string message);

}
