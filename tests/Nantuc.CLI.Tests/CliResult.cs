namespace TestsCLI;

public sealed record CliResult(int ExitCode, string Stdout, string Stderr);
