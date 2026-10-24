namespace Nantuc.VmApplication.Project;

public class VmHostProjectBuilder
{
    private IProjectFilesConfig? _projectPaths;
    private IFileService? _fileService;

    public VmHostProjectBuilder WithPaths(IProjectFilesConfig paths)
    {
        _projectPaths = paths;
        return this;
    }
    public VmHostProjectBuilder WithFileService(IFileService proj)
    {
        _fileService = proj;
        return this;
    }

    public VmHostProject Build()
    {
        return _projectPaths == null || _fileService == null
            ? throw new InvalidOperationException("ProjectPaths, Projectservice, Logger обязательны")
            : new VmHostProject(_projectPaths, _fileService);
    }
}
