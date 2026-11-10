using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication.Extensions.Host;

public static class ProjectExtension
{
    extension(VmHostProject proj)
    {
        public CompilationToIRResult CompileToIR(ulong baseAddress, bool optimize)
        {
            var files = ProjectBuilder.GetSourceFiles(proj.FileService, proj.Paths);
            return proj.CompileToIR(files, baseAddress, optimize);
        }

        public CompilationResult Compile(ulong baseAddress, bool optimize)
        {
            var files = ProjectBuilder.GetSourceFiles(proj.FileService, proj.Paths);
            return proj.Compile(files, baseAddress, optimize);

        }


        public CompilationToIRResult CompileToIR(string source, ulong baseAddress, bool optimize, SourceLanguage language)
            => proj.CompileToIR([new SourceFile("source", source, language)], baseAddress, optimize);

        public CompilationToIRResult CompileToIR(IEnumerable<string> sources, ulong baseAddress, bool optimize, SourceLanguage language)
            => proj.CompileToIR(
                [.. sources.Select((s, i) => new SourceFile($"source_{i}", s, language))],
                baseAddress,
                optimize);


        public CompilationResult Compile(string source, ulong baseAddress, bool optimize, SourceLanguage language)
            => proj.Compile([new SourceFile("source", source, language)], baseAddress, optimize);

        public CompilationResult Compile(IEnumerable<string> sources, ulong baseAddress, bool optimize, SourceLanguage language)
            => proj.Compile(
                [.. sources.Select((s, i) => new SourceFile($"source_{i}", s, language))],
                baseAddress,
                optimize);

        public void SaveFileInclude(string localPath,string content)
        {
            var fs = proj.FileService;
            var paths = proj.Paths;
            fs.SaveFile(
                fs.Combine(paths.IncludePath, localPath),
                content);
        }
        public void SaveFileProject(string localPath,string content)
        {
            var fs = proj.FileService;
            var paths = proj.Paths;
            fs.SaveFile(
                fs.Combine(paths.ProjectPath, localPath),
                content);
        }
    }
}
