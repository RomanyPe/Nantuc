namespace Nantuc.VmApplication.Project;

/// <summary>
/// Конфигурация путей
/// </summary>
public interface IProjectFilesConfig
{
    /// <summary>
    /// Базовая директория
    /// </summary>
    string BaseDirectory { get; }
    /// <summary>
    /// Путь до проекта
    /// </summary>
    string ProjectPath { get; }
    /// <summary>
    /// Путь до include
    /// </summary>
    string IncludePath { get; }
    /// <summary>
    /// Поддерживаемые расширения для nanasm
    /// </summary>
    IReadOnlyList<string> ExtensionsAsm { get; }
    /// <summary>
    /// Поддерживаемые расширения для MiniC
    /// </summary>
    IReadOnlyList<string> ExtensionsMiniC { get; }

    /// <summary>
    /// Стандартная конфигурация
    /// </summary>
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

