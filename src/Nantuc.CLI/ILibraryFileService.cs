using Nantuc.VmApplication.Project;
using System.Diagnostics.CodeAnalysis;

namespace Nantuc.CLI;

public interface ILibraryFileService : IFileService
{
    // Возвращает имена всех инклюдов в IncludePath
    IEnumerable<string> ListLibraries();

    // Добавляет инклюд с указанным именем и содержимым.
    // Возвращает false, если инклюд уже существует.
    bool AddLibrary(string name, string content);

    // Удаляет инклюд по имени.
    // Возвращает false, если инклюд не найден.
    bool RemoveLibrary(string name);

    // Проверяет существование инклюда.
    bool LibraryExists(string name);

    /// <summary>
    /// Разрешает относительный путь в абсолютный по правилам файловой системы сервиса.
    /// Возвращает <c>false</c>, если путь некорректен (недопустимые символы, слишком длинный и т.п.).
    /// </summary>
    bool TryGetFullPath(string path, [NotNullWhen(true)] out string? fullPath);
}
