using Nantuc.Contracts;

namespace Nantuc.Compiler.C.Optimizators;

public interface ILogOptimization : IReadOnlyLogOptimization
{
    void Clear();
    void Append(int pass, string message);

}
