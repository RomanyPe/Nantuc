namespace Nantuc.VmApplication.Project;

public interface IFileService
{
    IEnumerable<string> GetNameFiles();
    string ReadFile(string fileName);
    bool Exist(string fileName);
    string ReadIncludes(string fileName, IProjectFilesConfig pathConfig);
    void SaveFile(string fileName, string content);
    string Combine(string path1, string path2);
}