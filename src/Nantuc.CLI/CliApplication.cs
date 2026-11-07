using Nantuc.CLI.Commands;
using Nantuc.VmApplication.Project;

namespace Nantuc.CLI;

public sealed class CliApplication
{
    private readonly ILibraryFileService _fileService;
    private readonly IProjectFilesConfig _projectConfig;
    private readonly TextWriter _out;
    private readonly TextWriter _error;

    private CliApplication(ILibraryFileService fs, IProjectFilesConfig cfg, TextWriter @out, TextWriter err)
        => (_fileService, _projectConfig, _out, _error) = (fs, cfg, @out, err);

    public static CliApplication Create(ILibraryFileService fileService, IProjectFilesConfig projectConfig,
                                        TextWriter output, TextWriter error)
        => new(fileService, projectConfig, output, error);

    public int Run(ReadOnlySpan<string> args)
    {
        if (args.IsEmpty)
        {
            _error.WriteLine("No command. Try: nantuc help");
            return -1;
        }

        var command = args[0];
        var rest = args[1..];

        return command switch
        {
            "compile" or "nancl" or "build"=> RunCompile(rest),
            "run" => RunRun(rest),
            "disasm" => RunDisassemble(rest),
            "lib" or "include" => RunLib(rest),
            "help" or "h" => PrintHelp(),
            _ => Unknown(args),
        };
    }

    private int RunDisassemble(ReadOnlySpan<string> args)
    {
        if (args.IsEmpty)
        {
            _error.WriteLine("disasm: no args");
            return -4;
        }

        return !DisassembleCommand.TryCreate(args, _fileService, _out, _error, out var cmd) ? -2 : cmd.Execute();
    }

    private int RunLib(ReadOnlySpan<string> args)
    {
        if (args.IsEmpty)
        {
            _error.WriteLine("lib: не указано действие. Используйте add, remove, exist или list.");
            return -1;
        }

        string action = args[0];
        var rest = args[1..];

        var command = new LibCommand(_fileService, _out, _error);
        return command.Execute(action, rest);
    }

    private int RunCompile(ReadOnlySpan<string> args)
    {
        if (args.IsEmpty)
        {
            _error.WriteLine("compile: no args");
            return -4;
        }

        if (!CompileCommand.TryCreate(args, _fileService, _out, _projectConfig, out var cmd))
        {
            _error.WriteLine("compile: invalid arguments");
            return -2;
        }
        return cmd.Execute();
    }

    private int RunRun(ReadOnlySpan<string> args)
    {
        if (args.IsEmpty)
        {
            _error.WriteLine("run: no args");
            return -4;
        }

        if (!RunCommand.TryCreate(args, _fileService, _error, out var cmd))
        {
            _error.WriteLine("run: invalid arguments");
            return -2;
        }
        return cmd.Execute();
    }

    private int PrintHelp()
    {
        _out.WriteLine(CliMessages.UsageText);
        return 0;
    }
    private int Unknown(ReadOnlySpan<string> args)
    {
        _error.Write("Unknown command");
        if (!args.IsEmpty) { _error.Write(": "); _error.Write(string.Join(' ', args.ToArray())); }
        _error.WriteLine();
        return -3;
    }
}