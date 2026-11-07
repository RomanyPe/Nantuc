using Nantuc.VmApplication.Project;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Text;

namespace Nantuc.CLI;

/// <summary>
/// Реализация <see cref="ILibraryFileService"/> поверх абстракции <see cref="IFileSystem"/>.
/// Один и тот же класс работает с <c>RealFileSystem</c> (диск) и <c>MockFileSystem</c> (память).
/// </summary>
public sealed class FileSystemLibraryFileService : ILibraryFileService
{
    private readonly IFileSystem _fs;
    private readonly IProjectFilesConfig _config;
    private readonly string _includePath;

    public FileSystemLibraryFileService(IFileSystem fs, IProjectFilesConfig config)
    {
        _fs = fs ?? throw new ArgumentNullException(nameof(fs));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _includePath = config.IncludePath;
        _fs.Directory.CreateDirectory(_includePath); // гарантируем существование
    }

    // ---------- ILibraryFileService ----------
    public bool TryGetFullPath(string path, [NotNullWhen(true)] out string? fullPath)
    {
        try
        {
            fullPath = _fs.Path.GetFullPath(path);
            return true;
        }
        catch (Exception ex) when (
            ex is ArgumentException
               or NotSupportedException
               or PathTooLongException
               or System.Security.SecurityException)
        {
            fullPath = null;
            return false;
        }
    }
    public IEnumerable<string> ListLibraries()
    {
        if (!_fs.Directory.Exists(_includePath))
            return [];

        return _fs.Directory.GetFiles(_includePath)
            .Select(p => _fs.Path.GetFileName(p))
            .OfType<string>();
    }

    public bool AddLibrary(string name, string content)
    {
        var fullPath = _fs.Path.Combine(_includePath, name);
        if (_fs.File.Exists(fullPath))
            return false;

        _fs.File.WriteAllText(fullPath, content, Encoding.UTF8);
        return true;
    }

    public bool RemoveLibrary(string name)
    {
        var fullPath = _fs.Path.Combine(_includePath, name);
        if (!_fs.File.Exists(fullPath))
            return false;

        _fs.File.Delete(fullPath);
        return true;
    }

    public bool LibraryExists(string name)
    {
        var fullPath = _fs.Path.Combine(_includePath, name);
        return _fs.File.Exists(fullPath);
    }

    // ---------- IFileService ----------

    public IEnumerable<string> GetNameFiles(IProjectFilesConfig path)
    {
        var result = new List<string>();

        if (_fs.Directory.Exists(path.ProjectPath))
            result.AddRange(_fs.Directory.GetFiles(path.ProjectPath)
                .Select(p => _fs.Path.GetFileName(p))
                .OfType<string>());

        if (_fs.Directory.Exists(path.IncludePath))
            result.AddRange(_fs.Directory.GetFiles(path.IncludePath)
                .Select(p => _fs.Path.GetFileName(p))
                .OfType<string>());

        return result;
    }

    public string ReadFile(string fileName)
    {
        var includeFile = _fs.Path.Combine(_includePath, fileName);
        if (_fs.File.Exists(includeFile))
            return _fs.File.ReadAllText(includeFile, Encoding.UTF8);

        var projectFile = _fs.Path.Combine(_config.ProjectPath, fileName);
        if (_fs.File.Exists(projectFile))
            return _fs.File.ReadAllText(projectFile, Encoding.UTF8);

        throw new FileNotFoundException(
            $"File '{fileName}' not found in include or project paths.");
    }

    public bool Exist(string fileName)
    {
        return _fs.File.Exists(_fs.Path.Combine(_includePath, fileName)) ||
               _fs.File.Exists(_fs.Path.Combine(_config.ProjectPath, fileName));
    }

    public void SaveFile(string fileName, string content)
    {
        var target = _fs.File.Exists(_fs.Path.Combine(_includePath, fileName))
            ? _fs.Path.Combine(_includePath, fileName)
            : _fs.Path.Combine(_config.ProjectPath, fileName);

        var dir = _fs.Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(dir))
            _fs.Directory.CreateDirectory(dir);

        _fs.File.WriteAllText(target, content, Encoding.UTF8);
    }

    public string Combine(string path1, string path2)
        => _fs.Path.Combine(path1, path2);
}