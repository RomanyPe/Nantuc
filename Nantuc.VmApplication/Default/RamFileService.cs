using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication.Default;

public class RamFileService(IProjectFilesConfig projectFilesConfig) : IFileService
{
    private readonly Dictionary<string, string> _files = [];
    private readonly IProjectFilesConfig _projectFilesConfig = projectFilesConfig;
    public string Combine(string path1, string path2) => path1 + "\\" + path2;

    public bool Exist(string fileName) => _files.ContainsKey(fileName);

    public IEnumerable<string> GetNameFiles()
    {
        foreach (var file in _files.Keys)
        {
            if (file.StartsWith(_projectFilesConfig.IncludePath)) continue;
            yield return file;
        }
    }

    public string ReadFile(string fileName) => _files[fileName];
    public void SaveFile(string fileName, string content) => _files[fileName] = content;
    public string ReadIncludes(string fileName, IProjectFilesConfig pathConfig)
    {

        string fullPath = Combine(pathConfig.ProjectPath, fileName);
        string fullPathInc = Combine(pathConfig.IncludePath, fileName);

        bool projExist = Exist(fullPath);
        bool incExist = Exist(fullPathInc);

        if (projExist && incExist)
            throw new InvalidOperationException($"The file {fileName} exists in both {pathConfig.ProjectPath} and {pathConfig.IncludePath}; please select a specific file.");


        if (projExist)
        {
            return ReadFile(fullPath);
        }
        else if (incExist)
        {
            return ReadFile(fullPathInc);
        }

        throw new FileNotFoundException(
                $"Included file not found: '{fileName}'. Searched in paths: {pathConfig.ProjectPath}, {pathConfig.IncludePath}");
    }
}
