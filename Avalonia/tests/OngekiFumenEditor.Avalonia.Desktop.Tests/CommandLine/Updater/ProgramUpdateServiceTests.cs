using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Updater;
using OngekiFumenEditor.Avalonia.Utils;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine.Updater;

public sealed class ProgramUpdateServiceTests
{
    [Fact]
    public async Task Update_Success_RecursivelyCopiesIncludedFilesExcludesThreeExtensionsAndRestartsDesktop()
    {
        using var fixture = new UpdateFixture();
        var rootFile = fixture.AddSourceFile("root.txt", "new root");
        var nestedFile = fixture.AddSourceFile(Path.Combine("nested", "app.dll"), "new nested");
        var newFile = fixture.AddSourceFile("new.bin", "new file");
        var ignoredLog = fixture.AddSourceFile("ignored.log", "log");
        var ignoredXml = fixture.AddSourceFile(Path.Combine("nested", "ignored.xml"), "xml");
        var ignoredDump = fixture.AddSourceFile("ignored.dmp", "dump");
        var ignoredUppercaseLog = fixture.AddSourceFile("ignored-uppercase.LOG", "uppercase log");
        fixture.AddTargetFile("root.txt", "old root");
        fixture.AddTargetFile(Path.Combine("nested", "app.dll"), "old nested");

        var result = await fixture.CreateService(
        [
            rootFile,
            nestedFile,
            newFile,
            ignoredLog,
            ignoredXml,
            ignoredDump,
            ignoredUppercaseLog
        ]).UpdateAsync(fixture.Options);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("new root", fixture.ReadTargetFile("root.txt"));
        Assert.Equal("new nested", fixture.ReadTargetFile(Path.Combine("nested", "app.dll")));
        Assert.Equal("new file", fixture.ReadTargetFile("new.bin"));
        Assert.False(File.Exists(fixture.TargetFile("ignored.log")));
        Assert.False(File.Exists(fixture.TargetFile(Path.Combine("nested", "ignored.xml"))));
        Assert.False(File.Exists(fixture.TargetFile("ignored.dmp")));
        Assert.False(File.Exists(fixture.TargetFile("ignored-uppercase.LOG")));
        Assert.Empty(fixture.FindBackupFiles());
        Assert.Equal(
            Path.Combine(fixture.TargetPath, "OngekiFumenEditor.Avalonia.Desktop.exe"),
            fixture.ProcessEnvironment.StartedFileName);
        Assert.Equal(
            new[] { "--wait", "--notifySucess", "--sourceVersion", fixture.Options.SourceVersion },
            fixture.ProcessEnvironment.StartedArguments);
    }

    [Fact]
    public async Task Update_KillFails_ReturnsMinusOneBeforeAnyFileMutation()
    {
        using var fixture = new UpdateFixture();
        var sourceFile = fixture.AddSourceFile("app.dll", "new");
        fixture.AddTargetFile("app.dll", "old");
        fixture.ProcessEnvironment.ProcessIds = [fixture.ProcessEnvironment.CurrentProcessId, 20];
        fixture.ProcessEnvironment.KillFailureProcessId = 20;

        var result = await fixture.CreateService([sourceFile]).UpdateAsync(fixture.Options);

        Assert.Equal(-1, result.ExitCode);
        Assert.Contains("pid: 20", result.Message, StringComparison.Ordinal);
        Assert.Equal("old", fixture.ReadTargetFile("app.dll"));
        Assert.Empty(fixture.FindBackupFiles());
        Assert.Null(fixture.ProcessEnvironment.StartedFileName);
    }

    [Fact]
    public async Task Update_BackupFails_ReturnsMinusTwoAndRestoresEarlierBackups()
    {
        using var fixture = new UpdateFixture();
        var firstSource = fixture.AddSourceFile("first.dll", "new first");
        var secondSource = fixture.AddSourceFile("second.dll", "new second");
        fixture.AddTargetFile("first.dll", "old first");
        fixture.AddTargetFile("second.dll", "old second");
        fixture.FileSystem.MoveFailure = (source, destination) =>
            source == fixture.TargetFile("second.dll") && destination.Contains(".bak_", StringComparison.Ordinal);

        var result = await fixture.CreateService([firstSource, secondSource]).UpdateAsync(fixture.Options);

        Assert.Equal(-2, result.ExitCode);
        Assert.Contains("second.dll", result.Message, StringComparison.Ordinal);
        Assert.Equal("old first", fixture.ReadTargetFile("first.dll"));
        Assert.Equal("old second", fixture.ReadTargetFile("second.dll"));
        Assert.Empty(fixture.FindBackupFiles());
        Assert.Equal(0, fixture.FileSystem.CopyInvocationCount);
        Assert.Null(fixture.ProcessEnvironment.StartedFileName);
    }

    [Fact]
    public async Task Update_CopyFails_ReturnsMinusThreeAndPreservesLegacyPartialRollbackState()
    {
        using var fixture = new UpdateFixture();
        var firstSource = fixture.AddSourceFile("first.dll", "new first");
        var secondSource = fixture.AddSourceFile("second.dll", "new second");
        fixture.AddTargetFile("first.dll", "old first");
        fixture.AddTargetFile("second.dll", "old second");
        fixture.FileSystem.CopyFailure = (source, _) => source == secondSource;

        var result = await fixture.CreateService([firstSource, secondSource]).UpdateAsync(fixture.Options);

        Assert.Equal(-3, result.ExitCode);
        Assert.Contains("second.dll", result.Message, StringComparison.Ordinal);
        Assert.Equal("new first", fixture.ReadTargetFile("first.dll"));
        var remainingBackup = Assert.Single(fixture.FindBackupFiles());
        Assert.StartsWith("first.dll.bak_", Path.GetFileName(remainingBackup), StringComparison.Ordinal);
        Assert.Equal("old first", File.ReadAllText(remainingBackup));
        Assert.Equal("old second", fixture.ReadTargetFile("second.dll"));
        Assert.Null(fixture.ProcessEnvironment.StartedFileName);
    }

    [Fact]
    public async Task Update_BackupCleanupFails_StillReturnsSuccessAndRestartsDesktop()
    {
        using var fixture = new UpdateFixture();
        var sourceFile = fixture.AddSourceFile("app.dll", "new");
        fixture.AddTargetFile("app.dll", "old");
        fixture.FileSystem.DeleteFailure = path => path.Contains(".bak_", StringComparison.Ordinal);

        var result = await fixture.CreateService([sourceFile]).UpdateAsync(fixture.Options);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("new", fixture.ReadTargetFile("app.dll"));
        Assert.Single(fixture.FindBackupFiles());
        Assert.NotNull(fixture.ProcessEnvironment.StartedFileName);
    }

    [Fact]
    public async Task Update_Success_KillsOnlyOtherDesktopProcessesAndUsesLegacyNotifyArguments()
    {
        using var fixture = new UpdateFixture();
        var sourceFile = fixture.AddSourceFile(
            DefaultProgramUpdateService.DesktopExecutableName,
            "stub");
        fixture.ProcessEnvironment.ProcessIds =
        [
            fixture.ProcessEnvironment.CurrentProcessId,
            101,
            202
        ];

        var result = await fixture.CreateService([sourceFile]).UpdateAsync(fixture.Options);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("OngekiFumenEditor.Avalonia.Desktop", fixture.ProcessEnvironment.RequestedProcessName);
        Assert.Equal(new[] { 101, 202 }, fixture.ProcessEnvironment.KilledProcessIds);
        Assert.DoesNotContain(fixture.ProcessEnvironment.CurrentProcessId, fixture.ProcessEnvironment.KilledProcessIds);
        Assert.Equal(
            new[] { "--wait", "--notifySucess", "--sourceVersion", "9.8.7.6" },
            fixture.ProcessEnvironment.StartedArguments);
    }

    private sealed class UpdateFixture : IDisposable
    {
        public string RootPath { get; } = Path.Combine(
            Path.GetTempPath(),
            "OngekiFumenEditor.ProgramUpdateServiceTests",
            Guid.NewGuid().ToString("N"));
        public string SourcePath => Path.Combine(RootPath, "source");
        public string TargetPath => Path.Combine(RootPath, "target");
        public ControlledFileSystem FileSystem { get; } = new();
        public RecordingProcessEnvironment ProcessEnvironment { get; } = new();
        public UpdaterOption Options { get; }

        public UpdateFixture()
        {
            Directory.CreateDirectory(SourcePath);
            Directory.CreateDirectory(TargetPath);
            Log.Initialize(new Log([]));
            Options = new UpdaterOption
            {
                SourceFolder = SourcePath,
                TargetFolder = TargetPath,
                SourceVersion = "9.8.7.6"
            };
        }

        public DefaultProgramUpdateService CreateService(IEnumerable<string> sourceFiles)
        {
            FileSystem.SourceFiles = sourceFiles.ToArray();
            return new DefaultProgramUpdateService(FileSystem, ProcessEnvironment);
        }

        public string AddSourceFile(string relativePath, string content) =>
            WriteFile(SourcePath, relativePath, content);
        public string AddTargetFile(string relativePath, string content) =>
            WriteFile(TargetPath, relativePath, content);
        public string TargetFile(string relativePath) => Path.Combine(TargetPath, relativePath);
        public string ReadTargetFile(string relativePath) => File.ReadAllText(TargetFile(relativePath));
        public string[] FindBackupFiles() =>
            Directory.GetFiles(TargetPath, "*.bak_*", SearchOption.AllDirectories);

        private static string WriteFile(string rootPath, string relativePath, string content)
        {
            var filePath = Path.Combine(rootPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, content);
            return filePath;
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
    }

    private sealed class ControlledFileSystem : IProgramUpdateFileSystem
    {
        public string[] SourceFiles { get; set; } = [];
        public Func<string, string, bool>? MoveFailure { get; set; }
        public Func<string, string, bool>? CopyFailure { get; set; }
        public Func<string, bool>? DeleteFailure { get; set; }
        public int CopyInvocationCount { get; private set; }

        public IEnumerable<string> GetFiles(string path, string searchPattern, SearchOption searchOption) => SourceFiles;
        public void CreateDirectory(string path) => Directory.CreateDirectory(path);
        public bool FileExists(string path) => File.Exists(path);

        public void MoveFile(string sourceFilePath, string destinationFilePath)
        {
            if (MoveFailure?.Invoke(sourceFilePath, destinationFilePath) == true)
                throw new IOException("Injected move failure.");
            File.Move(sourceFilePath, destinationFilePath);
        }

        public void CopyFile(string sourceFilePath, string destinationFilePath)
        {
            CopyInvocationCount++;
            if (CopyFailure?.Invoke(sourceFilePath, destinationFilePath) == true)
                throw new IOException("Injected copy failure.");
            File.Copy(sourceFilePath, destinationFilePath);
        }

        public void DeleteFile(string path)
        {
            if (DeleteFailure?.Invoke(path) == true)
                throw new IOException("Injected delete failure.");
            File.Delete(path);
        }
    }

    private sealed class RecordingProcessEnvironment : IProgramUpdateProcessEnvironment
    {
        public int CurrentProcessId { get; } = 10;
        public int[] ProcessIds { get; set; } = [];
        public int? KillFailureProcessId { get; set; }
        public string? RequestedProcessName { get; private set; }
        public List<int> KilledProcessIds { get; } = [];
        public string? StartedFileName { get; private set; }
        public string[]? StartedArguments { get; private set; }

        public IEnumerable<int> GetProcessIdsByName(string processName)
        {
            RequestedProcessName = processName;
            return ProcessIds;
        }

        public void KillProcess(int processId)
        {
            if (KillFailureProcessId == processId)
                throw new InvalidOperationException("Injected kill failure.");
            KilledProcessIds.Add(processId);
        }

        public void StartProcess(string fileName, IReadOnlyList<string> arguments)
        {
            StartedFileName = fileName;
            StartedArguments = arguments.ToArray();
        }
    }
}
