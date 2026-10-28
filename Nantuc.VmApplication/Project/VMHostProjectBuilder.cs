using Nantuc.Kernel.Diagnostics;

namespace Nantuc.VmApplication.Project;

/// <summary> 
/// Билдер для <see cref="VmHostProject"/>
/// </summary>
public class VmHostProjectBuilder
{
    private IProjectFilesConfig? _projectPaths;
    private IFileService? _fileService;
    private VmCompilerOptimizator? _compilerOptimizator;
    private IErrorMessageProvider? _throwCompiler;

    /// <summary>
    /// Создать с определенным <see cref="IProjectFilesConfig"/>
    /// </summary>
    public VmHostProjectBuilder WithPaths(IProjectFilesConfig paths)
    {
        _projectPaths = paths;
        return this;
    }
    /// <summary>
    /// Создать с определенным <see cref="IFileService"/>
    /// </summary>
    public VmHostProjectBuilder WithFileService(IFileService proj)
    {
        _fileService = proj;
        return this;
    }
    /// <summary>
    /// Создать с готовыми оптимизаторами
    /// </summary>
    /// <param name="optimizator"></param>
    /// <returns></returns>
    public VmHostProjectBuilder WithOptimizator(VmCompilerOptimizator? optimizator)
    {
        _compilerOptimizator = optimizator;
        return this;
    }
    /// <summary>
    /// Создать с определенной политикой исключений
    /// </summary>
    /// <param name="policy"></param>
    public VmHostProjectBuilder WithThrowProvider(IErrorMessageProvider? policy)
    {
        _throwCompiler = policy;
        return this;
    }
    /// <summary>
    /// Собрать <see cref="VmHostProject"/> из имеющихся данных
    /// </summary
    public VmHostProject Build()
    {
        return _projectPaths == null || _fileService == null
            ? throw new InvalidOperationException("IProjectFilesConfig (WithPaths) и IFileService (WithFileService) обязательны для сборки VmHostProject")
            : new VmHostProject(new CompilerContext(_compilerOptimizator ?? new(), _projectPaths, _fileService, new(_throwCompiler)));
    }
}
