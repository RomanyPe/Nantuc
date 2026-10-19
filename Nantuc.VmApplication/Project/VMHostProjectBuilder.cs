namespace Nantuc.VmApplication.Project;

public class VMHostProjectBuilder
{
    private IProjectFilesConfig? _projectPaths;
    private IFileService? _fileService;

    public VMHostProjectBuilder WithPaths(IProjectFilesConfig paths)
    {
        _projectPaths = paths;
        return this;
    }
    public VMHostProjectBuilder WithFileService(IFileService proj)
    {
        _fileService = proj;
        return this;
    }

    public VMHostProject Build()
    {
        return _projectPaths == null || _fileService == null
            ? throw new InvalidOperationException("ProjectPaths, Projectservice, Logger обязательны")
            : new VMHostProject(_projectPaths, _fileService);
    }
}
