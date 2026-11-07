using Nantuc.VmApplication.Project;

namespace Nantuc.CLI;

public class ProjectsPathsConfig(string includePath) : IProjectFilesConfig
{
    public string BaseDirectory => Directory.GetCurrentDirectory();
    public string ProjectPath => BaseDirectory;
    public string IncludePath => includePath;
    public IReadOnlyList<string> ExtensionsAsm { get; } = Array.AsReadOnly([".asm", ".nanasm"]);
    public IReadOnlyList<string> ExtensionsMiniC { get; } = Array.AsReadOnly([".c", ".minic"]);
}
