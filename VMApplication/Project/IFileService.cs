namespace VMApplication.Project;

public interface IFileService
{
    IEnumerable<string> GeNameFiles();
    string ReadFile(string fileName);
    bool Exist(string fileName);
    string ReadIncludes(string fileName, IProjectFilesConfig pathConfig);
    public void SafeFile(string fileName, string content);
}