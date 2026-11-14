using Nantuc.Contracts;

namespace Nantuc.Compiler.C.Optimizations;

public interface IRogOptimization : IReadOnlyLogOptimization
{
    void Clear();
    void Append(int pass, string message);

}
