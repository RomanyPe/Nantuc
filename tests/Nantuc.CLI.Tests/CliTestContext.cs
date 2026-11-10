using Testably.Abstractions.Testing;

namespace Nantuc.CLI.Tests;

public sealed class CliTestContext
{
    public static string DefaultRoot => OperatingSystem.IsWindows()
        ? @"C:\work\app"
        : "/work/app";

    public MockFileSystem FileSystem { get; }
    public ProjectPathConfig Config { get; }
    public FileSystemLibraryFileService Service { get; }

    public CliTestContext(string? root = null)
    {
        root ??= DefaultRoot;

        FileSystem = new MockFileSystem();
        FileSystem.Directory.CreateDirectory(root);
        FileSystem.Directory.SetCurrentDirectory(root);

        var includePath = FileSystem.Path.Combine(root, "include");
        Config = new ProjectPathConfig(root, includePath);
        Service = new FileSystemLibraryFileService(FileSystem, Config);
    }

    // --- запуск CLI ---
    public CliResult Run(params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        int exit = CliApplication
            .Create(Service, Config, stdout, stderr)
            .Run(args);
        return new CliResult(exit, stdout.ToString(), stderr.ToString());
    }

    public CliResult RunLib(string action, params string[] rest)
        => Run(["lib", action, .. rest]);

    // --- helpers для файлов ---
    public string ProjectPath(string name) => FileSystem.Path.Combine(Config.ProjectPath, name);
    public string IncludePath(string name) => FileSystem.Path.Combine(Config.IncludePath, name);

    public void WriteProjectFile(string name, string content)
        => FileSystem.File.WriteAllText(ProjectPath(name), content);

    public void WriteIncludeFile(string name, string content)
        => FileSystem.File.WriteAllText(IncludePath(name), content);

    public bool ProjectFileExists(string name) => FileSystem.File.Exists(ProjectPath(name));
    public bool IncludeFileExists(string name) => FileSystem.File.Exists(IncludePath(name));
}