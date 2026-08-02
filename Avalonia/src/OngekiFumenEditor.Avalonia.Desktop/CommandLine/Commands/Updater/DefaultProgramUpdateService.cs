using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Updater;

[RegisterSingleton<IProgramUpdateService>]
internal sealed class DefaultProgramUpdateService : IProgramUpdateService
{
    internal const string DesktopProcessName = "OngekiFumenEditor.Avalonia.Desktop";
    internal const string DesktopExecutableName = "OngekiFumenEditor.Avalonia.Desktop.exe";

    private readonly IProgramUpdateFileSystem fileSystem;
    private readonly IProgramUpdateProcessEnvironment processEnvironment;

    public DefaultProgramUpdateService(
        IProgramUpdateFileSystem fileSystem,
        IProgramUpdateProcessEnvironment processEnvironment)
    {
        this.fileSystem = fileSystem;
        this.processEnvironment = processEnvironment;
    }

    public Task<ProgramUpdateResult> UpdateAsync(
        UpdaterOption option,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(option);

        var targetFolder = option.TargetFolder;
        var sourceVersion = option.SourceVersion;
        var sourceFolder = option.SourceFolder /*Path.GetDirectoryName(typeof(DefaultProgramUpdater).Assembly.Location)*/;

        var bakSuffix = $".bak_{RandomHepler.RandomString(10)}";
        Log.LogInfo($"sourceFolder: {sourceFolder}");

        //Dic<full,relative>
        var moveFiles = fileSystem.GetFiles(sourceFolder, "*.*", SearchOption.AllDirectories)
            .Where(path =>
            {
                //filter unused files by extension
                return Path.GetExtension(path).ToLowerInvariant() switch
                {
                    ".log" or
                    ".xml" or
                    ".dmp" => false,
                    _ => true
                };
            })
            .Select(path => Path.GetRelativePath(sourceFolder, path))
            .ToList();

        foreach (var directory in moveFiles.GroupBy(Path.GetDirectoryName).Select(group => group.Key))
            fileSystem.CreateDirectory(Path.Combine(targetFolder, directory));

        void DoRollback()
        {
            Log.LogInfo("rollback begin");
            foreach (var relativePath in moveFiles)
            {
                var targetFilePath = Path.Combine(targetFolder, relativePath);
                var targetBackupFilePath = Path.Combine(targetFolder, relativePath + bakSuffix);

                try
                {
                    if (fileSystem.FileExists(targetBackupFilePath))
                    {
                        fileSystem.MoveFile(targetBackupFilePath, targetFilePath);
                        Log.LogInfo($"* rollback file: {targetBackupFilePath} -> {targetFilePath}");
                    }
                }
                catch (Exception exception)
                {
                    Log.LogError($"rollback file failed: {targetBackupFilePath} -> {targetFilePath}", exception);
                }
            }
            Log.LogInfo("rollback end");
        }

        //setup enviorment
        //kill others editor processes
        var curPid = processEnvironment.CurrentProcessId;
        foreach (var processId in processEnvironment.GetProcessIdsByName(DesktopProcessName)
                     .Where(processId => curPid != processId))
        {
            try
            {
                processEnvironment.KillProcess(processId);
                Log.LogInfo($"other editor killed, pid: {processId}");
            }
            catch (Exception)
            {
                return Task.FromResult(new ProgramUpdateResult(-1, $"can't kill other editor, pid: {processId}"));
            }
        }

        //backup files which will be replaced.
        foreach (var relativePath in moveFiles)
        {
            var targetFilePath = Path.Combine(targetFolder, relativePath);
            var targetBackupFilePath = Path.Combine(targetFolder, relativePath + bakSuffix);

            try
            {
                if (fileSystem.FileExists(targetFilePath))
                {
                    fileSystem.MoveFile(targetFilePath, targetBackupFilePath);
                    Log.LogInfo($"* backup file: {targetFilePath} -> {targetBackupFilePath}");
                }
            }
            catch (Exception)
            {
                DoRollback();
                return Task.FromResult(new ProgramUpdateResult(
                    -2,
                    $"backup file failed: {targetFilePath} -> {targetBackupFilePath}"));
            }
        }

        //move files!
        foreach (var relativePath in moveFiles)
        {
            var sourceFilePath = Path.Combine(sourceFolder, relativePath);
            var targetFilePath = Path.Combine(targetFolder, relativePath);

            try
            {
                fileSystem.CopyFile(sourceFilePath, targetFilePath);
                Log.LogInfo($"* move file: {sourceFilePath} -> {targetFilePath}");
            }
            catch (Exception)
            {
                DoRollback();
                return Task.FromResult(new ProgramUpdateResult(
                    -3,
                    $"move file failed: {sourceFilePath} -> {targetFilePath}"));
            }
        }

        //delete backup files
        foreach (var relativePath in moveFiles)
        {
            var targetBackupFilePath = Path.Combine(targetFolder, relativePath + bakSuffix);

            try
            {
                fileSystem.DeleteFile(targetBackupFilePath);
                Log.LogInfo($"* delete backup file: {targetBackupFilePath}");
            }
            catch (Exception exception)
            {
                Log.LogError($"delete backup file failed: {targetBackupFilePath}", exception);
            }
        }

        //start program and notify user result
        var targetProgram = Path.Combine(targetFolder, DesktopExecutableName);
        processEnvironment.StartProcess(
            targetProgram,
            ["--wait", "--notifySucess", "--sourceVersion", sourceVersion]);

        return Task.FromResult(new ProgramUpdateResult(0));
    }
}
