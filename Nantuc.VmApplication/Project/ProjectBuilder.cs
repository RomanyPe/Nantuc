using Nantuc.Compiler.ASM;
using Nantuc.Compiler.ASM.Base;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C;
using Nantuc.Compiler.C.CodeGenerator;
using Nantuc.Compiler.C.Optimizators;
using Nantuc.Kernel.Contracts;
using Nantuc.Kernel.Diagnostics;

namespace Nantuc.VmApplication.Project;

public static class ProjectBuilder
{
    public const int BaseAddressProgram = 0x00;

    internal static IEnumerable<IReadOnlyLogOptimization> Build(IFileService fileService, IProjectFilesConfig paths, IEnumerable<SourceFile> files, bool optimize, AssemblerBase assembler, AstOptimizer astOptimizer)
    {
        var asmParser = new AssemblerParser();
        var cAsts = new List<ProgramNode>();
        var asmIncludes = new List<string>();

        var processedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files.Where(file => file.Language == SourceLanguage.C))
        {
            var ast = ParseCFile(file.Content);
            cAsts.Add(ast);

            ResolveIncludesRecursive(ast.IncludesList, processedIncludes, cAsts, asmIncludes, fileService, paths);
        }

        var combinedAst = new ProgramNode();
        foreach (var ast in cAsts)
        {
            combinedAst.FunctionNodes.AddRange(ast.FunctionNodes);
            combinedAst.GlobalVarNodes.AddRange(ast.GlobalVarNodes);
            combinedAst.StructNodes.AddRange(ast.StructNodes);
        }

        IEnumerable<IReadOnlyLogOptimization> resLog = optimize
            ? astOptimizer.Optimize(combinedAst)
            : [];

        var structLayouts = StructLayout.Resolve(combinedAst.StructNodes);

        var mainFunc = combinedAst.FunctionNodes.FirstOrDefault(f => f.Name == "main");
        if (mainFunc != null)
        {
            combinedAst.FunctionNodes.Remove(mainFunc);
            combinedAst.FunctionNodes.Insert(0, mainFunc);
        }

        if (combinedAst.FunctionNodes.Any(f => f.Name == "main"))
        {
            assembler.EmitJmp("__global_init");
        }

        foreach (string inc in asmIncludes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string asmCode = ReadInclude(fileService, inc, paths);
            asmParser.Assemble(asmCode, assembler);
        }

        foreach (SourceFile file in files.Where(file => file.Language == SourceLanguage.Asm))
        {
            asmParser.Assemble(file.Content, assembler);
        }

        var funcGen = new FunctionGenerator(assembler, structLayouts);
        funcGen.Generate(combinedAst);

        assembler.EmitEnd();

        return resLog;
    }

    private static void ResolveIncludesRecursive(IEnumerable<string> includes, HashSet<string> processed, List<ProgramNode> cAsts, List<string> asmIncludes, IFileService fileService, IProjectFilesConfig paths)
    {
        foreach (var inc in includes)
        {
            if (!processed.Add(inc)) continue; // Уже обработали

            string code = ReadInclude(fileService, inc, paths);
            SourceLanguage lang = FindLang(paths, inc);

            switch (lang)
            {
                case SourceLanguage.C:
                    var ast = ParseCFile(code);
                    cAsts.Add(ast);
                    ResolveIncludesRecursive(ast.IncludesList, processed, cAsts, asmIncludes, fileService, paths);
                    break;
                    
                case SourceLanguage.Asm: 
                    asmIncludes.Add(inc); 
                    break;

                case SourceLanguage.None:
                default:
                    ThrowHelper.ThrowMiniC(ErrorCode.NotSupported, $"extension for include \"{inc}\""); 
                    break;
            }
        }
    }

    private static ProgramNode ParseCFile(string content)
    {
        var tokens = new Lexer(content).Tokenize();
        return new Parser(tokens).Parse();
    }

    public static string ReadInclude(IFileService service, string fileName, IProjectFilesConfig pathConfig)
    {
        string projPath = pathConfig.ProjectPath;
        string includePath = pathConfig.IncludePath;

        string fullPath = service.Combine(projPath, fileName);
        string fullPathInc = service.Combine(includePath, fileName);

        bool projExist = service.Exist(fullPath);
        bool incExist = service.Exist(fullPathInc);

        if (projExist && incExist)
            throw new InvalidOperationException($"The file {fileName} exists in both {projPath} and {includePath}; please select a specific file.");


        if (projExist)
        {
            return service.ReadFile(fullPath);
        }
        else if (incExist)
        {
            return service.ReadFile(fullPathInc);
        }

        throw new FileNotFoundException($"Included file not found: '{fileName}'. Searched in paths: {projPath}, {includePath}");
    }

    public static IEnumerable<SourceFile> GetSourceFiles(IFileService fileService, IProjectFilesConfig path)
    {
        var files = new List<SourceFile>();

        foreach (string fileName in fileService.GetNameFiles(path))
        {
            SourceLanguage lang = FindLang(path, fileName);
            if (lang == SourceLanguage.None) continue;

            string source = fileService.ReadFile(fileName);
            files.Add(new SourceFile(fileName, source, lang));
        }
        return files;
    }

    private static bool IsAsm(IProjectFilesConfig config, string name)
    {
        var extensions = config.ExtensionsAsm;
        return HasExtension(extensions, Path.GetExtension(name));
    }

    private static bool IsMiniC(IProjectFilesConfig config, string name)
    {
        var extensions = config.ExtensionsMiniC;
        return HasExtension(extensions, Path.GetExtension(name));
    }

    private static bool HasExtension(IReadOnlyList<string> extensions, string fileExt)
    {
        foreach (var ext in extensions)
        {
            if (string.Equals(ext, fileExt, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
    private static SourceLanguage FindLang(IProjectFilesConfig config, string name)
    {
        if (IsAsm(config, name)) return SourceLanguage.Asm;
        else if (IsMiniC(config, name)) return SourceLanguage.C;
        else return SourceLanguage.None;
    }
}

