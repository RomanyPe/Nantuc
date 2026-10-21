using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication.Default;

public sealed class DefaultFileService : IFileService
{
    public IEnumerable<string> GetNameFiles(IProjectFilesConfig pathConfig)
    {
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);


        AddRange(extensions, pathConfig.ExtensionsMiniC);
        AddRange(extensions, pathConfig.ExtensionsAsm);

        return Directory.EnumerateFiles(pathConfig.ProjectPath, "*.*", SearchOption.AllDirectories)
            .Where(f => extensions.Contains(Path.GetExtension(f)));
    }
    
    private static void AddRange(HashSet<string> extensions, IReadOnlyList<string> exts)
    {
        foreach (var ext in exts)
            extensions.Add(ext);
    }
    public string ReadFile(string filePath) => File.ReadAllText(filePath);
    public void SaveFile(string filePath, string content) => File.WriteAllText(filePath, content);
    public bool Exist(string filePath) => File.Exists(filePath);
    public string Combine(string path1, string path2) => Path.Combine(path1, path2);
}
