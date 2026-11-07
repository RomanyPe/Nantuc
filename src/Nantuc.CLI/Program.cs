using Nantuc.CLI;
using Testably.Abstractions;

var paths = new ProjectsPathsConfig(Path.Combine(AppContext.BaseDirectory, "include"));
var fs = new RealFileSystem();
fs.Directory.CreateDirectory(paths.IncludePath);

return CliApplication
    .Create(
        fileService: new FileSystemLibraryFileService(fs, paths),
        projectConfig: paths,
        output: Console.Out,
        error: Console.Error)
    .Run(args);