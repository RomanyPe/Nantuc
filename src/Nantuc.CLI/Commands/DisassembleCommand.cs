using Nantuc.Common;
using Nantuc.VmApplication.Extensions.Disassembler;
using Nantuc.VmApplication.Project;
using System.Diagnostics.CodeAnalysis;

namespace Nantuc.CLI.Commands;

public class DisassembleCommand(
    TextWriter wr,
    IFileService fileService,
    string path,
    int? codeSize,
    string? savePath = null)
{
    public static bool TryCreate(
        ReadOnlySpan<string> args,
        IFileService fileService,
        TextWriter output,
        TextWriter error,
        [NotNullWhen(true)] out DisassembleCommand? command)
    {
        command = null;

        int? codeSizen = null;
        if (CLIHelper.TryGetArgByFlag(["-cs", "-codes"], args, out var codeSizeStr))
        {
            if (!int.TryParse(codeSizeStr, out int codeSize) || codeSize < 0)
            {
                error.WriteLine($"disasm: invalid code size '{codeSizeStr}'");
                return false;
            }
            codeSizen = codeSize;
        }

        if (!CLIHelper.TryGetArgByFlag(["-f", "-file"], args, out var path))
        {
            error.WriteLine("disasm: missing required flag '-f' / '-file'");
            return false;
        }

        if (!fileService.Exist(path))
        {
            error.WriteLine($"disasm: file '{path}' does not exist");
            return false;
        }

        CLIHelper.TryGetArgByFlag(["-s", "-save"], args, out var savePath);

        command = new DisassembleCommand(output, fileService, path, codeSizen, savePath);
        return true;
    }

    public int Execute()
    {
        var bytes = CLIHelper.GetBytesFromText(fileService.ReadFile(path));
        codeSize ??= bytes.Length;
        var text = StringDisassembler.Disassemble(bytes, codeSize.Value);

        if (savePath is null)
        {
            wr.WriteLine(text);
            return 0;
        }

        var target = fileService.Combine(savePath, "result-decompile.txt");
        fileService.SaveFile(target, text);
        wr.WriteLine($"result saved in: {target}");
        return 0;
    }
}