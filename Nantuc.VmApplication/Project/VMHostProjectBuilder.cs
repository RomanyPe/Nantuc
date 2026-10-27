namespace Nantuc.VmApplication.Project;

/// <summary> 
/// Билдер для <see cref="VmHostProject"/>
/// </summary>
public class VmHostProjectBuilder
{
    private IProjectFilesConfig? _projectPaths;
    private IFileService? _fileService;
    private VmCompilerOptimizator? _compilerOptimizator;

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
    public VmHostProjectBuilder WithOptimizator(VmCompilerOptimizator? optimizator)
    {
        _compilerOptimizator = optimizator;
        return this;
    }
    /// <summary>
    /// Собрать <see cref="VmHostProject"/> из имеющихся данных
    /// </summary
    public VmHostProject Build()
    {
        return _projectPaths == null || _fileService == null
            ? throw new InvalidOperationException("IProjectFilesConfig (WithPaths) и IFileService (WithFileService) обязательны для сборки VmHostProject")
            : new VmHostProject(_projectPaths, _fileService, _compilerOptimizator ?? new());
    }
}
