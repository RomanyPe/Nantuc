namespace Nantuc.VmApplication.Project;

/// <summary>
/// Контракт файлового сервиса
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Метод получения всех названий файлов
    /// </summary>
    /// <param name="path">конфигурация путей по которым искать</param>
    IEnumerable<string> GetNameFiles(IProjectFilesConfig path);
    /// <summary>
    /// Чтения файла по названию (относительному пути)
    /// </summary>
    /// <param name="fileName">название файла</param>
    string ReadFile(string fileName);
    /// <summary>
    /// Проверка существует ли файл в сервисе
    /// </summary>
    /// <param name="fileName">название файла</param>
    bool Exist(string fileName);
    /// <summary>
    /// Сохраняет файл в сервис (не используется системой)
    /// </summary>
    /// <param name="fileName">название файла</param>
    /// <param name="content">контент который нужно сохранить</param>
    void SaveFile(string fileName, string content);
    /// <summary>
    /// Комбинирование 2 путей в 1
    /// </summary>
    /// <param name="path1">путь 1</param>
    /// <param name="path2">путь 2</param>
    /// <returns>итоговый путь</returns>
    string Combine(string path1, string path2);
}