using Nantuc.Common;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.DeviceCtx;
using Nantuc.VmApplication.Extensions.Emulator.IO;
using Nantuc.VmApplication.Project;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Text;

namespace Nantuc.CLI.Commands;

public class RunCommand(string path, PortSize portSize, DevicePortSize devicePortSize, RamSize ram, long limit, bool snowTimer, string? pathToDumbMemory, string? pathToDumbRegisters, TextWriter wr, IFileSystem fs, IFileService file)
{
    private static bool HaveTimerArg(ReadOnlySpan<string> args) => CLIHelper.HaveFlag(["-timer", "-lt", "-t"], args);
    
    private static DevicePortSize GetDevicePortSize(ReadOnlySpan<string> args)
    {
        DevicePortSize dps = DevicePortSize.B16;

        if (CLIHelper.TryGetArgByFlag(["-dps", "-device-per-port", "-device-on-port"], args, out var str) && RunCommandHelpers.TryParseDevicePortSize(str, out var devicePortSize) && devicePortSize.IsValid)
            dps = devicePortSize;
        return dps;
    }
    private static PortSize GetPortSize(ReadOnlySpan<string> args)
    {
        PortSize ps = PortSize.KB1;

        if (CLIHelper.TryGetArgByFlag(["-ps", "-port-size", "-port"], args, out var str1) && RunCommandHelpers.TryParsePortSize(str1, out var portSize) && portSize.IsValid)
            ps = portSize;
        return ps;
    }

    private static RamSize GetRamSize(ReadOnlySpan<string> args)
    {
        RamSize rm = RamSize.MB1;

        if (CLIHelper.TryGetArgByFlag(["-ram", "-ram-size", "-r"], args, out var str2) && RunCommandHelpers.TryParseRamSize(str2, out var ramSize) && ramSize.IsValid)
            rm = ramSize;
        return rm;
    }

    private static long GetLimitSteps(ReadOnlySpan<string> args)
    {
        long limit = long.MaxValue;
        if (CLIHelper.TryGetArgByFlag(["-l", "-limit"], args, out var str3) && long.TryParse(str3, out var d) && d > 0)
            limit = d;
        return limit;
    }


    public int Execute()
    {
        ReadOnlySpan<byte> prog = CLIHelper.GetBytesFromText(file.ReadFile(path));

        using var emulator = new VmEmulatorBuilder()
            .WithPortBusSize(portSize)
            .WithPortsPerDevice(devicePortSize)
            .Build();

        var io = new SynchronousIOStream(Console.Out, PortCharEncoding.Utf16);
        var spec = DeviceSpec.Create(ram);
        var deviceId = emulator.RegisterDevice(spec, 0);
        emulator.RegisterDevice(io, "iostream", 1);
        var ctx = emulator.GetDeviceContext(deviceId)!;

        if (!ctx.TryLoadProgram(prog, 0, out var err))
        {
            wr.WriteLine(err);
            return -1;
        }

        ProcessorFault res = ctx.Run(new LaunchOptions(
            0, limit, snowTimer,
            OnEnd: c => wr.WriteLine($"Total steps: {c.Steps}, Elapsed: {c.Elapsed:mm\\:ss\\.fff}")));

        if (res.Status != BiosStatus.EndProgram)
        {
            wr.WriteLine($"Fault status:{RunCommandHelpers.ToDisplayString(res.Status)}, " +
                         $"fault data: {res.FaultData}, ip: {res.InstructionPointer}, " +
                         $"opcode {res.OpCode.OpName()}");
        }

        var regs = ctx.GetRegistersSnapshot();

        if (!string.IsNullOrWhiteSpace(pathToDumbMemory))
            DumpMemory(ctx, pathToDumbMemory!);

        if (!string.IsNullOrWhiteSpace(pathToDumbRegisters))
            DumpRegisters(regs, pathToDumbRegisters!);

        return (int)regs[RegType.r0];
    }

    private void DumpMemory(DeviceContext ctx, string filePath)
    {
        EnsureParentDirectory(filePath);
        fs.File.WriteAllBytes(filePath, ctx.GetAllRamAsNewArray());
    }

    private void DumpRegisters(SnapshotRegisters regs, string filePath)
    {
        EnsureParentDirectory(filePath);

        var sb = new StringBuilder();
        for (RegType i = 0; i < (RegType)32; i++)
        {
            sb.Append(i.RegName()).Append(": ").Append(regs[i]).Append('\n');
        }

        fs.File.WriteAllText(filePath, sb.ToString());
    }

    private void EnsureParentDirectory(string filePath)
    {
        var parent = fs.Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(parent) && !fs.Directory.Exists(parent))
            fs.Directory.CreateDirectory(parent);
    }
    public static bool TryCreate(
        ReadOnlySpan<string> args,
        IFileService fileService,
        IFileSystem fileSystem,
        TextWriter error,
        [NotNullWhen(true)] out RunCommand? command)
    {
        if (!CLIHelper.TryGetArgByFlag(["-p", "-prog"], args, out var path))
        {
            error.WriteLine("run: path to program is missing (use -p <file>)");
            command = null;
            return false;
        }

        if (!fileService.Exist(path))
        {
            error.WriteLine($"run: file '{path}' does not exist");
            command = null;
            return false;
        }

        if (!TryGetOptionalFilePath(args, fileSystem, error,
                ["-dm", "-dumbm", "-dumb-memory"], "-dm", out var pathToDumbMemory))
        {
            command = null;
            return false;
        }

        if (!TryGetOptionalFilePath(args, fileSystem, error,
                ["-dr", "-dumbr", "-dumb-registers"], "-dr", out var pathToDumbRegisters))
        {
            command = null;
            return false;
        }

        command = new RunCommand(
            path,
            GetPortSize(args),
            GetDevicePortSize(args),
            GetRamSize(args),
            GetLimitSteps(args),
            HaveTimerArg(args),
            pathToDumbMemory,
            pathToDumbRegisters,
            error,
            fileSystem,
            fileService);

        return true;
    }

    private static bool TryGetOptionalFilePath(
        ReadOnlySpan<string> args,
        IFileSystem fs,
        TextWriter error,
        string[] flags,
        string flagName,
        out string? value)
    {
        value = null;

        // Флаг не указан — это нормально, дамп не нужен.
        if (!CLIHelper.HaveFlag(flags, args))
            return true;

        // Флаг есть, но без значения / пустой.
        if (!CLIHelper.TryGetArgByFlag(flags, args, out value) || string.IsNullOrWhiteSpace(value))
        {
            error.WriteLine($"run: {flagName} requires a non-empty file path");
            return false;
        }

        // Явно указали директорию разделителем на конце.
        if (value.EndsWith(fs.Path.DirectorySeparatorChar)
            || value.EndsWith(fs.Path.AltDirectorySeparatorChar))
        {
            error.WriteLine($"run: {flagName} expects a file path, not a directory: '{value}'");
            return false;
        }

        // Уже существует и это директория — не наша цель.
        if (fs.Directory.Exists(value))
        {
            error.WriteLine($"run: {flagName} points to an existing directory, expected a file: '{value}'");
            return false;
        }

        return true;
    }
}
