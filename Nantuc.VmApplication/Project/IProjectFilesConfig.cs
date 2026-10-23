namespace Nantuc.VmApplication.Project;

public interface IProjectFilesConfig
{
    string BaseDirectory { get; }
    string ProjectPath { get; }
    string IncludePath { get; }
    IReadOnlyList<string> ExtensionsAsm { get; }
    IReadOnlyList<string> ExtensionsMiniC { get; }

    public static IProjectFilesConfig Default { get; } = new DefaultProjectFilesConfig();

    private sealed class DefaultProjectFilesConfig : IProjectFilesConfig
    {
        public string BaseDirectory { get; } = AppContext.BaseDirectory;
        public string ProjectPath { get; } = Path.Combine(AppContext.BaseDirectory, "project");
        public string IncludePath { get; } = Path.Combine(AppContext.BaseDirectory, "include");
        public IReadOnlyList<string> ExtensionsAsm { get; } = Array.AsReadOnly([".asm", ".nanasm"]);
        public IReadOnlyList<string> ExtensionsMiniC { get; } = Array.AsReadOnly([".c", ".minic"]);
    }
}

