using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication.Default;

/// <summary>
/// Класс реализующий <see cref="IFileService"/>, хранит файлы на озу
/// </summary>
public class RamFileService : IFileService
{
    private readonly Dictionary<string, string> _files = [];
    public string Combine(string path1, string path2) => Path.Combine(path1, path2);

    public bool Exist(string fileName) => _files.ContainsKey(fileName);

    public IEnumerable<string> GetNameFiles(IProjectFilesConfig path)
    {
        return _files.Keys.Where(file => !file.StartsWith(path.IncludePath, StringComparison.Ordinal) && IsCorrectly(file, path));
    }

    private static bool IsCorrectly(string file, IProjectFilesConfig filesConfig)
    {
        return Ext(file, filesConfig.ExtensionsMiniC) || Ext(file, filesConfig.ExtensionsAsm);
    }
    private static bool Ext(string file, IReadOnlyList<string> extensions)
    {
        foreach (var ext in extensions)
        {
            if (file.EndsWith(ext, StringComparison.Ordinal)) return true;
        }
        return false;
    }
    public string ReadFile(string fileName) => _files[fileName];
    public void SaveFile(string fileName, string content) => _files[fileName] = content;
}
