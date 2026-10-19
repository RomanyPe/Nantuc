using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication.Default;

public sealed class DefaultFileService(string? projectPath = null) : IFileService
{

    private readonly string _projectPath = string.IsNullOrEmpty(projectPath) 
        ? AppDomain.CurrentDomain.BaseDirectory 
        : projectPath;

    public IEnumerable<string> GetNameFiles()
    {
        return Directory.EnumerateFiles(_projectPath, "*.*", SearchOption.AllDirectories);
    }

    public string ReadFile(string fileName)
    {
        var fullPath = Path.Combine(_projectPath, fileName);
        return File.ReadAllText(fullPath);
    }

    public void SafeFile(string fileName, string content)
    {
        var fullPath = Path.Combine(_projectPath, fileName);
        File.WriteAllText(fullPath, content);
    }
    public bool Exist(string fileName)
    {
        var fullPath = Path.Combine(_projectPath, fileName);
        return File.Exists(fullPath);
    }

    public string ReadIncludes(string fileName, IProjectFilesConfig pathConfig)
    {
        string projPath = pathConfig.ProjectPath;
        string includePath = pathConfig.IncludePath;

        string fullPath = Path.Combine(projPath, fileName);
        string fullPathInc = Path.Combine(includePath, fileName);

        bool projExist = Exist(fullPath);
        bool incExist = Exist(fullPathInc);

        if (projExist && incExist)
            throw new InvalidOperationException($"The file {fileName} exists in both {projPath} and {includePath}; please select a specific file.");


        if (projExist)
        {
            return ReadFile(fullPath);
        }
        else if (incExist)
        {
            return ReadFile(fullPathInc);
        }

        throw new FileNotFoundException(
                $"Included file not found: '{fileName}'. Searched in paths: {projPath}, {includePath}");
    }
}
