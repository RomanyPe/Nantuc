namespace VMApplication.Project;

public interface IProjectFilesConfig
{
    string ProjectPath { get; }
    string IncludePath { get; }
    IReadOnlyList<string> ExtensionsAsm { get; }
    IReadOnlyList<string> ExtensionsMiniC { get; }

    public static IProjectFilesConfig Default { get; } = new DefaultProjectFilesConfig();

    private sealed class DefaultProjectFilesConfig : IProjectFilesConfig
    {
        public string ProjectPath { get; } = AppDomain.CurrentDomain.BaseDirectory;
        public string IncludePath { get; } = "include";
        public IReadOnlyList<string> ExtensionsAsm { get; } = [".asm", ".vma"];
        public IReadOnlyList<string> ExtensionsMiniC { get; } = [".c", ".mic"];
    }
}

