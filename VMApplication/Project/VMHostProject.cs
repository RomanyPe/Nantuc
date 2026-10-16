using Compiller.ASM.Optimizators;

namespace VMApplication.Project;

public readonly struct CompilationToILResult
{
    internal readonly IRAssembler? Assembler;
    public IReadOnlyList<string>? Errors { get; }
    internal OptimizationResultLog? OptimizationResultLog { get; }
    internal CompilationToILResult(IRAssembler? assembler, OptimizationResultLog? optimizationResultLog, IReadOnlyList<string>? errors = null)
    {
        Assembler = assembler;
        OptimizationResultLog = optimizationResultLog;
        Errors = errors;
    }

    public CompilationResult Build()
    {
        try
        {
            var asm = Assembler;
            return asm != null
                ? new CompilationResult(asm.Build(), asm.BaseAddress, null, OptimizationResultLog)
                : new CompilationResult(null, 0, Errors, null);
        }
        catch (Exception ex)
        {
            return new CompilationResult(null, 0, [ex.Message], null);
        }
    }
}

public sealed class VMHostProject(IProjectFilesConfig paths, IFileService fileService)
{
    private readonly IProjectFilesConfig _projectPaths = paths;
    private readonly IFileService _fileService = fileService;
    private readonly VMHostProjectCompiler _compiler = new(paths, fileService);
    public VMHostProjectCompiler Compiler => _compiler;

    public string IncludePath => _projectPaths.IncludePath;

    public IFileService FileService => _fileService;
    public IProjectFilesConfig ProjectFilesConfig => _projectPaths;

    public CompilationToILResult CompileToIL(ulong baseAddress, bool optimize)
           => _compiler.CompileToIL(baseAddress, optimize);
    public CompilationResult Compile(ulong baseAddress, bool optimize)
        => _compiler.Compile(baseAddress, optimize);
    public CompilationResult Compile(CompilationToILResult res, int peepholeOptimizationCount)
        => _compiler.Compile(res, peepholeOptimizationCount);
    public CompilationToILResult CompileToIL(IEnumerable<SourceFile> files, ulong baseAddress, bool optimize)
    => _compiler.CompileToIL(files, baseAddress, optimize);
    public CompilationToILResult CompileToIL(string source, ulong baseAddress, bool optimize, SourceLanguage language = SourceLanguage.C)
        => _compiler.CompileToIL(source, baseAddress, optimize, language);
    public CompilationToILResult CompileToIL(IEnumerable<string> sources, ulong baseAddress, bool optimize, SourceLanguage language = SourceLanguage.C)
        => _compiler.CompileToIL(sources, baseAddress, optimize, language);
    public CompilationResult Compile(IEnumerable<SourceFile> files, ulong baseAddress, bool optimize)
        => _compiler.Compile(files, baseAddress, optimize);
    public CompilationResult Compile(string source, ulong baseAddress, bool optimize, SourceLanguage language = SourceLanguage.C)
        => _compiler.Compile(source, baseAddress, optimize, language);
    public CompilationResult Compile(IEnumerable<string> sources, ulong baseAddress, bool optimize, SourceLanguage language = SourceLanguage.C)
        => _compiler.Compile(sources, baseAddress, optimize, language);
}
