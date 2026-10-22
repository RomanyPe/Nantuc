using Nantuc.Compiler.ASM;
using Nantuc.Compiler.ASM.Optimizators;
using Nantuc.Compiler.C;
using Nantuc.Compiler.C.CodeGenerator;
using Nantuc.Compiler.C.Optimizators;
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;

namespace Nantuc.VmApplication.Project;

public class ProjectBuilder(IFileService fileService, IProjectFilesConfig path)
{
    public const int BaseAddressProgram = 0x00;

    private readonly IFileService _fileService = fileService;
    private readonly IProjectFilesConfig _path = path;

    internal IEnumerable<IReadOnlyLogOptimization> Build(IEnumerable<SourceFile> files, bool optimize, AssemblerBase assembler, AstOptimizer astOptimizer)
    {
        var asmParser = new AssemblerParser();

        var cAsts = new List<ProgramNode>();
        var includes = new List<string>();
        foreach (var file in files.Where(file => file.Language == SourceLanguage.C))
        {
            var lexer = new Lexer(file.Content);
            var tokens = lexer.Tokenize();
            var parser = new Parser(tokens);
            var ast = parser.Parse();
            cAsts.Add(ast);
            includes.AddRange(ast.IncludesList);
        }
        var combinedAst = new ProgramNode();
        foreach (var ast in cAsts)
        {
            combinedAst.FunctionNodes.AddRange(ast.FunctionNodes);
            combinedAst.GlobalVarNodes.AddRange(ast.GlobalVarNodes);
            combinedAst.StructNodes.AddRange(ast.StructNodes);
        }

        IEnumerable<IReadOnlyLogOptimization> resLog = 
            optimize ? astOptimizer.Optimize(combinedAst) 
            : [];


        var allStructDecls = new List<StructDeclNode>();
        foreach (var ast in cAsts)
            allStructDecls.AddRange(ast.StructNodes);

        var structLayouts = StructLayout.Resolve(allStructDecls);

        var mainFunc = combinedAst.FunctionNodes.FirstOrDefault(f => f.Name == "main");
        if (mainFunc != null)
        {
            combinedAst.FunctionNodes.Remove(mainFunc);
            combinedAst.FunctionNodes.Insert(0, mainFunc);
        }

        if (combinedAst.FunctionNodes.Any(f => f.Name == "main"))
        {
            assembler.EmitJmp("func_main");
        }

        includes = [.. includes.Distinct(StringComparer.OrdinalIgnoreCase)];
        AddInclude(includes, asmParser, assembler);

        foreach (var file in files.Where(file => file.Language == SourceLanguage.Asm))
        {
            asmParser.Assemble(file.Content, assembler);
        }

        var funcGen = new FunctionGenerator(assembler, structLayouts);
        funcGen.Generate(combinedAst);

        assembler.EmitEnd();

        return resLog;
    }

    private void AddInclude(IEnumerable<string> includes, AssemblerParser asmParser, AssemblerBase assembler)
    {
        foreach (string inc in includes)
        {
            string asmCode = ReadIncludes(_fileService, inc, _path);
            asmParser.Assemble(asmCode, assembler);
        }
    }

    public static string ReadIncludes(IFileService service, string fileName, IProjectFilesConfig pathConfig)
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

        throw new FileNotFoundException(
                $"Included file not found: '{fileName}'. Searched in paths: {projPath}, {includePath}");
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

