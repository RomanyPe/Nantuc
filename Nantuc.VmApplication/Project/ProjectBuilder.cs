using Nantuc.Compiler.ASM;
using Nantuc.Compiler.ASM.Base;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C;
using Nantuc.Compiler.C.AST;
using Nantuc.Compiler.C.CodeGenerator;
using Nantuc.Compiler.C.Optimizators;
using Nantuc.Kernel.Contracts;
using Nantuc.Kernel.Diagnostics;

namespace Nantuc.VmApplication.Project;

/// <summary>
/// Билдер проекта, запускает парсинг, находит include, запускает оптимизациями, не хранит состояние
/// </summary>
public static class ProjectBuilder
{
    /// <summary>
    /// Значение 0x0, стартовый адрес для большинства программ
    /// </summary>
    public const int BaseAddressProgram = 0x00;

    internal static IEnumerable<IReadOnlyLogOptimization> Build(IFileService fileService, IProjectFilesConfig paths, IEnumerable<SourceFile> files, bool optimize, AssemblerBase assembler, AstOptimizer astOptimizer, ThrowCompiler throwCompiler)
    {
        var asmParser = new AssemblerParser();
        var cAsts = new List<ProgramNode>();
        var asmIncludes = new List<string>();

        var processedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files.Where(file => file.Language == SourceLanguage.C))
        {
            var ast = ParseCFile(file.Content, throwCompiler);
            cAsts.Add(ast);

            ResolveIncludesRecursive(ast.IncludesList, processedIncludes, cAsts, asmIncludes, fileService, paths, throwCompiler);
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

        var structLayouts = StructLayout.Resolve(combinedAst.StructNodes, throwCompiler);

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
            string asmCode = ReadInclude(fileService, inc, paths, throwCompiler);
            asmParser.Assemble(asmCode, assembler);
        }

        foreach (SourceFile file in files.Where(file => file.Language == SourceLanguage.Asm))
        {
            asmParser.Assemble(file.Content, assembler);
        }

        var funcGen = new FunctionGenerator(assembler, structLayouts, throwCompiler);
        funcGen.Generate(combinedAst);

        assembler.EmitEnd();

        return resLog;
    }

    private static void ResolveIncludesRecursive(IEnumerable<string> includes, HashSet<string> processed, List<ProgramNode> cAsts, List<string> asmIncludes, IFileService fileService, IProjectFilesConfig paths, ThrowCompiler throwCompiler)
    {
        foreach (var inc in includes)
        {
            if (!processed.Add(inc)) continue; // Уже обработали

            string code = ReadInclude(fileService, inc, paths, throwCompiler);
            SourceLanguage lang = FindLang(paths, inc);

            switch (lang)
            {
                case SourceLanguage.C:
                    var ast = ParseCFile(code, throwCompiler);
                    cAsts.Add(ast);
                    ResolveIncludesRecursive(ast.IncludesList, processed, cAsts, asmIncludes, fileService, paths, throwCompiler);
                    break;
                    
                case SourceLanguage.Asm: 
                    asmIncludes.Add(inc); 
                    break;

                case SourceLanguage.None:
                default:
                    throwCompiler.Throw(ErrorCode.NotSupported, $"extension for include \"{inc}\""); 
                    break;
            }
        }
    }

    private static ProgramNode ParseCFile(string content, ThrowCompiler throwCompiler)
    {
        var tokens = new Lexer(content, throwCompiler).Tokenize();
        return new Parser(tokens, throwCompiler).Parse();
    }

    /// <summary>
    /// Читает 1 include файл, сам находит его либо в <see cref="IProjectFilesConfig.ProjectPath"/>, либо в <see cref="IProjectFilesConfig.IncludePath"/>
    /// </summary>
    /// <param name="service">файловый сервис где происходит поиск</param>
    /// <param name="fileName">имя файла</param>
    /// <param name="pathConfig">файловая конфигурация</param>
    /// <returns>Контент файла если был найден</returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    public static string ReadInclude(IFileService service, string fileName, IProjectFilesConfig pathConfig, ThrowCompiler throwCompiler)
    {
        string projPath = pathConfig.ProjectPath;
        string includePath = pathConfig.IncludePath;

        string fullPath = service.Combine(projPath, fileName);
        string fullPathInc = service.Combine(includePath, fileName);

        bool projExist = service.Exist(fullPath);
        bool incExist = service.Exist(fullPathInc);

        if (projExist && incExist)
            throwCompiler.Throw(ErrorCode.Parser_IncludeAmbiguous, fileName, projPath, includePath);


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

    /// <summary>
    /// Метод для получения из <see cref="IFileService"/> и <see cref="IProjectFilesConfig"/> файлов исходного кода для компиляции
    /// Сам определяет язык по расширению файла, пропускает файлы у которых язык не определен
    /// </summary>
    /// <param name="fileService">файловый сервис</param>
    /// <param name="path">файловая конфигурация</param>
    /// <returns><see cref="IEnumerable{T}"/> файлов ввиде: <see cref="SourceFile"/></returns>
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

