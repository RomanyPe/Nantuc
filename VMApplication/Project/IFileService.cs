namespace VMApplication.Project;

public interface IFileService
{
    IEnumerable<string> GetSourceFiles();
    string ReadFile(string fileName);
    bool Exist(string fileName);
    string ReadIncludes(string fileName, IProjectFilesConfig pathConfig);

}