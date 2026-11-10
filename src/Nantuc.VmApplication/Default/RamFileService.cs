using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication.Default;

/// <summary>
/// Файловый сервис в ОЗУ, эмулирующий поведение реальной ФС
/// в рамках контракта <see cref="IFileService"/>.
///
/// Особенности эмуляции:
/// <list type="bullet">
/// <item>все пути канонизируются: оба разделителя (<c>\\</c> и <c>/</c>) приводятся
/// к одному, разрешаются <c>.</c> и <c>..</c>, убираются повторные и хвостовые разделители;</item>
/// <item>по умолчанию сравнение путей регистронезависимое (как на Windows);</item>
/// <item><see cref="Combine"/> ведёт себя как <see cref="Path.Combine"/> (с последующей канонизацией);</item>
/// <item>каталоги «создаются» неявно при сохранении файла по вложенному пути;</item>
/// <item><see cref="ReadFile"/> бросает исключение для несуществующего файла — как настоящий ФС.</item>
/// </list>
/// </summary>
public class RamFileService : IFileService
{
    private readonly Dictionary<string, string> _files;
    private readonly StringComparer _pathComparer;
    private readonly StringComparison _pathComparison;
    private readonly char _separator;

    /// <param name="caseSensitive">Регистрозависимость путей. По умолчанию <c>false</c> (как на Windows).</param>
    /// <param name="separator">Разделитель, который будет использоваться в канонических путях. Входные пути могут содержать оба вида.</param>
    public RamFileService(bool caseSensitive = false, char separator = '/')
    {
        _pathComparer = caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        _pathComparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        _files = new Dictionary<string, string>(_pathComparer);
        _separator = separator;
    }

    public string Combine(string path1, string path2)
        => Normalize(Path.Combine(path1, path2));

    public bool Exist(string fileName)
        => _files.ContainsKey(Normalize(fileName));

    public string ReadFile(string fileName)
        => _files[Normalize(fileName)];

    public void SaveFile(string fileName, string content)
        => _files[Normalize(fileName)] = content;

    public IEnumerable<string> GetNameFiles(IProjectFilesConfig path)
    {
        var includePath = Normalize(path.IncludePath);

        // Фильтрация сохранена из исходной реализации: файлы, путь которых
        // начинается с IncludePath, исключаются, остальные — проверяются по расширениям.
        return _files.Keys.Where(file =>
            !IsUnderPath(file, includePath, _pathComparison)
            && HasAllowedExtension(file, path));
    }

    private static bool HasAllowedExtension(string file, IProjectFilesConfig config)
        => HasAnyExtension(file, config.ExtensionsMiniC)
           || HasAnyExtension(file, config.ExtensionsAsm);

    private static bool HasAnyExtension(string file, IReadOnlyList<string> extensions)
    {
        if (extensions is null) return false;

        foreach (var ext in extensions)
        {
            if (!string.IsNullOrEmpty(ext) && file.EndsWith(ext, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Приводит путь к каноническому виду (по правилам, близким к реальной ФС):
    /// унифицирует разделители, разрешает <c>.</c> и <c>..</c>, убирает лишние разделители,
    /// сохраняет признак абсолютности (корень <c>/</c> или диск <c>C:</c>).
    /// </summary>
    private string Normalize(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        var unified = path.Replace('\\', '/');
        var hasDrive = unified.Length >= 2 && unified[1] == ':' && char.IsLetter(unified[0]);
        var isRooted = unified.StartsWith('/') || hasDrive;

        var parts = unified.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var stack = new List<string>(parts.Length);

        var index = 0;
        if (hasDrive)
        {
            stack.Add(parts.Length > 0 ? parts[0] : unified[..2]);
            index = 1;
        }

        for (; index < parts.Length; index++)
        {
            var part = parts[index];

            if (part == ".")
                continue;

            if (part == "..")
            {
                // Не выходим за корень: для абсолютного пути ".." в корне игнорируется.
                var minimumDepth = hasDrive ? 1 : 0;
                if (stack.Count > minimumDepth && stack[^1] != "..")
                    stack.RemoveAt(stack.Count - 1);
                else if (!isRooted)
                    stack.Add("..");

                continue;
            }

            stack.Add(part);
        }

        var joined = string.Join(_separator, stack);

        if (hasDrive)
            return joined;

        return isRooted ? _separator + joined : joined;
    }
    private static bool IsUnderPath(string file, string path, StringComparison comparison)
    {
        if (!file.StartsWith(path, comparison))
            return false;
        if (file.Length == path.Length)
            return true;
        var next = file[path.Length];
        return next == '/' || next == '\\';
    }
}