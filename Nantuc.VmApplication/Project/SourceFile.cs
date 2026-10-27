namespace Nantuc.VmApplication.Project;

/// <summary>
/// Структура - файл исходного кода
/// </summary>
/// <param name="Name">название файла</param>
/// <param name="Content">содержимое файла</param>
/// <param name="Language">язык файла (определяется по расширению)</param>
public readonly record struct SourceFile(string Name, string Content, SourceLanguage Language);

