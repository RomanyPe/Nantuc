namespace Nantuc.CLI;

public class CliMessages
{
    public const string UsageText = """
        Usage: nantuc <command> [options]

        Commands:
          compile   Compile source files into a program image
          run       Execute a compiled program in the VM emulator
          disasm    Disassemble a compiled program
          lib       Manage include libraries
          help, h   Show this help

        compile options:
          -s, -sv, -save <dir>       Directory where program.bin is written (required)
          -o, -opt                   Enable optimizations

        run options:
          -p, -prog <file>           Program image to run (required)
          -ram, -ram-size, -r <S>    RAM size (default: 1MB)
                                     64B 128B 256B 512B
                                     1KB 2KB 4KB 8KB 16KB 32KB 64KB 128KB 256KB 512KB
                                     1MB 2MB 4MB 8MB 16MB 32MB 64MB 128MB 256MB 512MB 1GB 2GB
          -ps, -port-size <S>        Port size (default: 1KB)
                                     64B 128B 256B 512B
                                     1KB 2KB 4KB 8KB 16KB 32KB 64KB 128KB 256KB 512KB
          -dps, -device-per-port <S> Ports per device (default: 16B)
                                     8B 16B 32B 64B 128B 256B
          -l, -limit <N>             Max steps to execute (default: unlimited)
          -t, -timer, -lt            Print elapsed time when the program ends
          -dm, -dumbm, -dumb-memory <file>     Save a memory dump to <file> when execution ends
          -dr, -dumbr, -dumb-registers <file>  Save a register dump to <file> when execution ends
                
        disasm options:
          -f, -file <file>           Program image to disassemble (required)
          -cs, -codes <N>            Number of bytes to disassemble (default: file size)
          -s, -save <dir>            Save output to <dir>/result-decompile.txt

        lib actions:
          add <name> <content>       Add a library from inline content
          add -p, -path <file>       Add a library from an external file
          remove <name>              Remove a library
          exist <name>               Check whether a library exists (exit code 0/1)
          list                       List all libraries

        Examples:
          nantuc compile -s bin -o
          nantuc run -p bin/program.bin -ram 4MB -l 100000
          nantuc disasm -f bin/program.bin -cs 64 -s out
          nantuc lib add -p ./extra.inc
          nantuc lib list
        """;
}
