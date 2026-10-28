using Nantuc.Kernel.Diagnostics;

namespace Nantuc.VmApplication.Project;

/// <summary>
/// Контекст компилятора, 1 на компилятор
/// </summary>
/// <param name="Optimizator">оптимизаторы</param>
/// <param name="Paths">файловая конфигурация</param>
/// <param name="FileService">файловый сервис</param>
/// <param name="ThrowCompiler">политика исключений</param>
public sealed record class CompilerContext(VmCompilerOptimizator Optimizator, IProjectFilesConfig Paths, IFileService FileService, ThrowCompiler ThrowCompiler);