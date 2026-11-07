using Nantuc.VmApplication.Project;

namespace TestsCLI;


public class ProjectPathConfig(string baseDirectory, string includePath) : IProjectFilesConfig
{
    public string BaseDirectory { get; } = baseDirectory;
    public string ProjectPath { get; } = baseDirectory;
    public string IncludePath { get; } = includePath;

    public IReadOnlyList<string> ExtensionsAsm { get; } = Array.AsReadOnly([".asm", ".nanasm"]);
    public IReadOnlyList<string> ExtensionsMiniC { get; } = Array.AsReadOnly([".c", ".minic"]);
}