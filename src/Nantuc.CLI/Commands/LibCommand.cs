namespace Nantuc.CLI.Commands;

public sealed class LibCommand(IRibraryFileService fileService, TextWriter output, TextWriter error)
{
    private readonly IRibraryFileService _fileService = fileService;
    private readonly TextWriter _output = output;
    private readonly TextWriter _error = error;

    public int Execute(string action, ReadOnlySpan<string> args)
    {
        return action switch
        {
            "add" => Add(args),
            "remove" => Remove(args),
            "exist" => Exist(args),
            "list" => List(),
            _ => Unknown(action)
        };
    }

    private int Add(ReadOnlySpan<string> args)
    {
        // Режим добавления из файла: nantuc lib add -p <путь>
        if (CLIHelper.HaveFlag(["-p", "-path"], args))
        {
            if (!CLIHelper.TryGetArgByFlag(["-p", "-path"], args, out var path))
            {
                _error.WriteLine("lib add: после -p / -path требуется путь к файлу");
                return -1;
            }

            if (!_fileService.TryGetFullPath(path, out var fullPath))
            {
                _error.WriteLine($"lib add: некорректный путь '{path}'");
                return -1;
            }

            if (!_fileService.Exist(fullPath))
            {
                _error.WriteLine($"lib add: файл '{fullPath}' не найден");
                return -1;
            }

            string content = _fileService.ReadFile(fullPath);
            string name = Path.GetFileName(fullPath);

            if (!_fileService.AddLibrary(name, content))
            {
                _error.WriteLine($"lib add: инклюд '{name}' уже существует");
                return -1;
            }

            _output.WriteLine($"Добавлен инклюд из файла: {name}");
            return 0;
        }
        // Режим добавления из строки: nantuc lib add <имя> <содержимое>
        if (args.Length < 2)
        {
            _error.WriteLine("lib add: требуется -p <путь> или <имя> <содержимое>");
            return -1;
        }

        string libName = args[0];
        string libContent = args[1];

        if (!_fileService.AddLibrary(libName, libContent))
        {
            _error.WriteLine($"lib add: инклюд '{libName}' уже существует");
            return -1;
        }

        _output.WriteLine($"Добавлен инклюд: {libName}");
        return 0;
    }
    private int Remove(ReadOnlySpan<string> args)
    {
        if (args.Length < 1)
        {
            _error.WriteLine("lib remove: требуется <имя>");
            return -1;
        }

        string name = args[0];
        if (!_fileService.RemoveLibrary(name))
        {
            _error.WriteLine($"lib remove: инклюд '{name}' не найден");
            return -1;
        }

        _output.WriteLine($"Удалён инклюд: {name}");
        return 0;
    }

    private int Exist(ReadOnlySpan<string> args)
    {
        if (args.Length < 1)
        {
            _error.WriteLine("lib exist: требуется <имя>");
            return -1;
        }

        string name = args[0];
        bool exists = _fileService.LibraryExists(name);
        _output.WriteLine(exists ? "Да" : "Нет");
        return exists ? 0 : 1;
    }

    private int List()
    {
        var libs = _fileService.ListLibraries().ToList();
        if (libs.Count == 0)
        {
            _output.WriteLine("Инклюды отсутствуют");
            return 0;
        }

        foreach (var lib in libs)
            _output.WriteLine(lib);

        return 0;
    }

    private int Unknown(string action)
    {
        _error.WriteLine($"lib: неизвестное действие '{action}'. Допустимо: add, remove, exist, list");
        return -1;
    }
}