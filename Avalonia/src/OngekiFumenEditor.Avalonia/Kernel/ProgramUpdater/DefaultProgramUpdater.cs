using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Kernel.Scheduler;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Reflection;

namespace OngekiFumenEditor.Avalonia.Kernel.ProgramUpdater;

[RegisterSingleton<IProgramUpdater>]
[RegisterSingleton<ISchedulable>]
internal class DefaultProgramUpdater : IProgramUpdater, ISchedulable
{
    private const string ApiEndPoint = "https://fumen.naominet.live";
    private readonly HttpClient http = new();

    public bool HasNewVersion
    {
        get
        {
            if (RemoteVersionInfo?.Version is not Version remoteVersion)
                return false;

            var localVersion = GetLocalVersion();
            return remoteVersion > localVersion;
        }
    }

    public VersionInfo RemoteVersionInfo { get; private set; }

    public string SchedulerName => "Program Update Check Scheduler";

    public TimeSpan ScheduleCallLoopInterval => TimeSpan.FromMinutes(5);

    private static Version GetLocalVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(DefaultProgramUpdater).Assembly;
        return assembly.GetName().Version ?? new Version(0, 0, 0, 0);
    }

    public async Task CheckUpdatable()
    {
        if (!ProgramSetting.Default.EnableUpdateCheck)
        {
            RemoteVersionInfo = null;
            return;
        }

        try
        {
            var url = $"{ApiEndPoint}/editor/getVersionInfo?requireMasterBranch={ProgramSetting.Default.UpdaterCheckMasterBranchOnly}";
            RemoteVersionInfo = await http.GetFromJsonAsync<VersionInfo>(url);
        }
        catch (Exception e)
        {
            Log.LogError($"Can't check update because exception: {e.Message}", e);
            RemoteVersionInfo = null;
        }
    }

    public async Task StartUpdate()
    {
        if (RemoteVersionInfo is null)
            throw new InvalidOperationException("Can't start update because RemoteVersionInfo is empty.");

        var isMaster = string.Equals(RemoteVersionInfo.Branch, "master", StringComparison.InvariantCultureIgnoreCase);
        var url = $"{ApiEndPoint}/editor/get?requireMasterBranch={isMaster}";

        using var zipStream = new MemoryStream();
        Log.LogInfo($"begin download editor zip file: {url}");

        using (var ns = await http.GetStreamAsync(url))
        {
            await ns.CopyToAsync(zipStream);
            zipStream.Seek(0, SeekOrigin.Begin);
        }

        var tempZipFolder = TempFileHelper.GetTempFolderPath("updater", $"{RemoteVersionInfo.Branch}_{RemoteVersionInfo.Version}");
        var sourceFolder = TempFileHelper.GetTempFolderPath("updater", $"{RemoteVersionInfo.Branch}_{RemoteVersionInfo.Version}_source");

        Log.LogInfo($"tempZipFolder = {tempZipFolder}");
        using var zipFile = new ZipArchive(zipStream, ZipArchiveMode.Read);
        zipFile.ExtractToDirectory(tempZipFolder, true);
        zipFile.ExtractToDirectory(sourceFolder, true);

        var updaterFilePath = Path.Combine(tempZipFolder, "OngekiFumenEditor.CommandLine.exe");
        if (!File.Exists(updaterFilePath))
            throw new FileNotFoundException($"Downloaded wrong file, updater file is not found: {updaterFilePath}", updaterFilePath);

        var targetFolder = Path.GetDirectoryName(typeof(DefaultProgramUpdater).Assembly.Location) ?? Environment.CurrentDirectory;
        var args = new[]
        {
            "updater", "-v",
            "--targetFolder", targetFolder,
            "--sourceFolder", sourceFolder,
            "--sourceVersion", GetLocalVersion().ToString()
        };

        Log.LogInfo($"updaterFilePath: {updaterFilePath}");
        Log.LogInfo($"targetFolder: {targetFolder}");
        Log.LogInfo($"args: {string.Join(" ", args)}");

        var psi = new ProcessStartInfo
        {
            FileName = updaterFilePath,
            UseShellExecute = false
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);
        Process.Start(psi);

        Log.LogInfo("update process started.");
    }

    public (int exitCode, string message) CommandExecuteUpdate(UpdaterOption option)
    {
        var targetFolder = option.TargetFolder;
        var sourceVersion = option.SourceVersion;
        var sourceFolder = option.SourceFolder;

        var bakSuffix = $".bak_{RandomHepler.RandomString(10)}";
        Log.LogInfo($"sourceFolder: {sourceFolder}");

        var moveFiles = Directory.GetFiles(sourceFolder, "*.*", SearchOption.AllDirectories)
            .Where(x =>
            {
                return Path.GetExtension(x).ToLowerInvariant() switch
                {
                    ".log" or ".xml" or ".dmp" => false,
                    _ => true
                };
            })
            .Select(x => Path.GetRelativePath(sourceFolder, x))
            .ToList();

        foreach (var dir in moveFiles.GroupBy(x => Path.GetDirectoryName(x)).Select(x => x.Key).Where(x => !string.IsNullOrWhiteSpace(x)))
            Directory.CreateDirectory(Path.Combine(targetFolder, dir));

        void DoRollback()
        {
            Log.LogInfo("rollback begin");
            foreach (var relativePath in moveFiles)
            {
                var targetFilePath = Path.Combine(targetFolder, relativePath);
                var targetBackupFilePath = Path.Combine(targetFolder, relativePath + bakSuffix);

                try
                {
                    if (File.Exists(targetBackupFilePath))
                    {
                        File.Move(targetBackupFilePath, targetFilePath, true);
                        Log.LogInfo($"* rollback file: {targetBackupFilePath} -> {targetFilePath}");
                    }
                }
                catch (Exception e)
                {
                    Log.LogError($"rollback file failed: {targetBackupFilePath} -> {targetFilePath}", e);
                }
            }
            Log.LogInfo("rollback end");
        }

        var curPid = Process.GetCurrentProcess().Id;
        foreach (var process in Process.GetProcessesByName("OngekiFumenEditor").Where(x => curPid != x.Id))
        {
            try
            {
                process.Kill();
                Log.LogInfo($"other editor killed, pid: {process.Id}");
            }
            catch
            {
                return (-1, $"can't kill other editor, pid: {process.Id}");
            }
        }

        foreach (var relativePath in moveFiles)
        {
            var targetFilePath = Path.Combine(targetFolder, relativePath);
            var targetBackupFilePath = Path.Combine(targetFolder, relativePath + bakSuffix);

            try
            {
                if (File.Exists(targetFilePath))
                {
                    File.Move(targetFilePath, targetBackupFilePath, true);
                    Log.LogInfo($"* backup file: {targetFilePath} -> {targetBackupFilePath}");
                }
            }
            catch
            {
                DoRollback();
                return (-2, $"backup file failed: {targetFilePath} -> {targetBackupFilePath}");
            }
        }

        foreach (var relativePath in moveFiles)
        {
            var sourceFilePath = Path.Combine(sourceFolder, relativePath);
            var targetFilePath = Path.Combine(targetFolder, relativePath);

            try
            {
                File.Copy(sourceFilePath, targetFilePath, true);
                Log.LogInfo($"* move file: {sourceFilePath} -> {targetFilePath}");
            }
            catch
            {
                DoRollback();
                return (-3, $"move file failed: {sourceFilePath} -> {targetFilePath}");
            }
        }

        foreach (var relativePath in moveFiles)
        {
            var targetBackupFilePath = Path.Combine(targetFolder, relativePath + bakSuffix);
            try
            {
                if (File.Exists(targetBackupFilePath))
                {
                    File.Delete(targetBackupFilePath);
                    Log.LogInfo($"* delete backup file: {targetBackupFilePath}");
                }
            }
            catch (Exception e)
            {
                Log.LogError($"delete backup file failed: {targetBackupFilePath}", e);
            }
        }

        var targetProgram = Path.Combine(targetFolder, "OngekiFumenEditor.Avalonia.exe");
        if (!File.Exists(targetProgram))
            targetProgram = Path.Combine(targetFolder, "OngekiFumenEditor.exe");

        if (File.Exists(targetProgram))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = targetProgram,
                UseShellExecute = false,
                ArgumentList = { "--wait", "--notifySucess", "--sourceVersion", sourceVersion }
            });
        }

        return (0, string.Empty);
    }

    public void OnSchedulerTerm()
    {
    }

    public async Task OnScheduleCall(CancellationToken cancellationToken)
    {
        await CheckUpdatable();
    }
}

