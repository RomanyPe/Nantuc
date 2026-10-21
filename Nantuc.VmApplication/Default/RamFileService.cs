using Nantuc.VmApplication.Project;
using System.Linq;

namespace Nantuc.VmApplication.Default;

public class RamFileService : IFileService
{
    private readonly Dictionary<string, string> _files = [];
    public string Combine(string path1, string path2) => path1 + "\\" + path2;

    public bool Exist(string fileName) => _files.ContainsKey(fileName);

    public IEnumerable<string> GetNameFiles(IProjectFilesConfig path)
    {
        return _files.Keys.Where(file => !file.StartsWith(path.IncludePath) && IsCorrectly(file, path));
    }

    private static bool IsCorrectly(string file, IProjectFilesConfig filesConfig)
    {
        return Ext(file, filesConfig.ExtensionsMiniC) || Ext(file, filesConfig.ExtensionsAsm);
    }
    private static bool Ext(string file, IReadOnlyList<string> extensions)
    {
        foreach (var ext in extensions)
        {
            if (file.EndsWith(ext)) return true;
        }
        return false;
    }
    public string ReadFile(string fileName) => _files[fileName];
    public void SaveFile(string fileName, string content) => _files[fileName] = content;
}
