using Nantuc.Contracts;

namespace Nantuc.Compiler.C.Optimizators;

public interface IRogOptimization : IReadOnlyLogOptimization
{
    void Clear();
    void Append(int pass, string message);

}
