namespace VMApplication.Project;

public interface IProjectFilesConfig
{
    string ProjectPath { get; }
    string IncludePath { get; }
    string[] ExtensionsAsm { get; }
    string[] ExtensionsMiniC { get; }

    public static IProjectFilesConfig Default { get; } = new DefaultProjectFilesConfig();

    private sealed class DefaultProjectFilesConfig : IProjectFilesConfig
    {
        public string ProjectPath { get; } = AppDomain.CurrentDomain.BaseDirectory;
        public string IncludePath { get; } = "include";
        public string[] ExtensionsAsm { get; } = [".asm", ".vma"];
        public string[] ExtensionsMiniC { get; } = [".c", ".mic"];
    }
}

