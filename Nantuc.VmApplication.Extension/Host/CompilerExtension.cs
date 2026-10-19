using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication.Extensions.Host;

public static class CompilerExtension
{
    extension(VMHostProject compiler)
    {
        public CompilationToILResult CompileToIL(ulong baseAddress, bool optimize)
        {
            var files = ProjectBuilder.GetSourceFiles(compiler.FileService, compiler.Paths);
            return compiler.CompileToIL(files, baseAddress, optimize);
        }

        public CompilationResult Compile(ulong baseAddress, bool optimize)
        {
            var files = ProjectBuilder.GetSourceFiles(compiler.FileService, compiler.Paths);
            return compiler.Compile(files, baseAddress, optimize);

        }


        public CompilationToILResult CompileToIL(string source, ulong baseAddress, bool optimize, SourceLanguage language)
            => compiler.CompileToIL([new SourceFile("source", source, language)], baseAddress, optimize);

        public CompilationToILResult CompileToIL(IEnumerable<string> sources, ulong baseAddress, bool optimize, SourceLanguage language)
            => compiler.CompileToIL(
                [.. sources.Select((s, i) => new SourceFile($"source_{i}", s, language))],
                baseAddress,
                optimize);


        public CompilationResult Compile(string source, ulong baseAddress, bool optimize, SourceLanguage language)
            => compiler.Compile([new SourceFile("source", source, language)], baseAddress, optimize);

        public CompilationResult Compile(IEnumerable<string> sources, ulong baseAddress, bool optimize, SourceLanguage language)
            => compiler.Compile(
                [.. sources.Select((s, i) => new SourceFile($"source_{i}", s, language))],
                baseAddress,
                optimize);

    }
}