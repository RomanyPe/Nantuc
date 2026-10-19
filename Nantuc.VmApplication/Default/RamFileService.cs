using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication.Default;

public class RamFileService : IFileService
{
    private readonly Dictionary<string, string> _files = [];

    private static string CombinePath(string path1, string path2) => path1 + "\\" + path2;

    public bool Exist(string fileName) => _files.ContainsKey(fileName);

    public IEnumerable<string> GetNameFiles() => _files.Keys;

    public string ReadFile(string fileName) => _files[fileName];
    public void SafeFile(string fileName, string content) => _files[fileName] = content;
    public string ReadIncludes(string fileName, IProjectFilesConfig pathConfig)
    {
        string projPath = pathConfig.ProjectPath;
        string includePath = pathConfig.IncludePath;

        string fullPath = CombinePath(projPath, fileName);
        string fullPathInc = CombinePath(includePath, fileName);

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
            return  ReadFile(fullPathInc);
        }

        throw new FileNotFoundException(
                $"Included file not found: '{fileName}'. Searched in paths: {projPath}, {includePath}");
    }
}
