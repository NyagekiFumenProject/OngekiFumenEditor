using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Regression;

public sealed class BrowserOpfsLogStorageContractTests
{
    [Fact]
    public void BrowserOpfsCoreResponsibilities_AreExtractedIntoOpfsModule()
    {
        string repositoryRoot = FindRepositoryRoot();
        string opfs = ReadBrowserScript(repositoryRoot, "opfs.js");
        string temporary = ReadBrowserScript(repositoryRoot, "temporaryFileSystem.js");
        string logs = ReadBrowserScript(repositoryRoot, "logFileSystem.js");
        string main = ReadBrowserScript(repositoryRoot, "main.js");

        Assert.Contains("originRoot = await storage.getDirectory();", opfs, StringComparison.Ordinal);
        Assert.Contains("export class OpfsDirectory", opfs, StringComparison.Ordinal);
        Assert.Contains("export async function getOrCreateRootDirectory", opfs, StringComparison.Ordinal);
        Assert.DoesNotContain("navigator.storage.getDirectory", temporary, StringComparison.Ordinal);
        Assert.DoesNotContain("getDirectoryHandle", temporary, StringComparison.Ordinal);
        Assert.DoesNotContain("navigator.storage.getDirectory", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("getDirectoryHandle", logs, StringComparison.Ordinal);
        Assert.Contains("from './opfs.js';", temporary, StringComparison.Ordinal);
        Assert.Contains("from './opfs.js';", logs, StringComparison.Ordinal);
        Assert.Contains("initialize as initializeOpfs", main, StringComparison.Ordinal);

        int opfsInitialization = main.IndexOf("await initializeOpfs();", StringComparison.Ordinal);
        Assert.True(opfsInitialization >= 0);
        int temporaryInitialization = main.IndexOf(
            "initializeTemporaryFileSystem(),",
            opfsInitialization,
            StringComparison.Ordinal);
        int logInitialization = main.IndexOf(
            "initializeLogFileSystem(),",
            opfsInitialization,
            StringComparison.Ordinal);
        Assert.True(temporaryInitialization > opfsInitialization);
        Assert.True(logInitialization > opfsInitialization);
    }

    [Fact]
    public void BrowserOpfsLogStorage_CreatesLogsFolderAtOriginRootInsteadOfTemporaryRoot()
    {
        string repositoryRoot = FindRepositoryRoot();
        string opfs = ReadBrowserScript(repositoryRoot, "opfs.js");
        string temporary = ReadBrowserScript(repositoryRoot, "temporaryFileSystem.js");
        string logs = ReadBrowserScript(repositoryRoot, "logFileSystem.js");
        string provider = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "Platforms",
            "Services",
            "Logging",
            "BrowserLogFileStorage.cs"));

        Assert.Contains(
            "requireOriginRoot().getDirectoryHandle(entryName, { create: true })",
            opfs,
            StringComparison.Ordinal);
        Assert.Contains("getOrCreateRootDirectory(\"temp\")", temporary, StringComparison.Ordinal);
        Assert.Contains("getOrCreateRootDirectory(\"logs\")", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("getOrCreateRootDirectory(\"logs\")", temporary, StringComparison.Ordinal);
        Assert.DoesNotContain("globalThis.LogFileSystemInterop", temporary, StringComparison.Ordinal);
        Assert.Contains("globalThis.LogFileSystemInterop", logs, StringComparison.Ordinal);
        Assert.Contains("const writeBuffers = new Map();", temporary, StringComparison.Ordinal);
        Assert.Contains("const writeBuffers = new Map();", logs, StringComparison.Ordinal);
        Assert.Contains("tryCreateFile,", logs, StringComparison.Ordinal);
        Assert.Contains("appendFile,", logs, StringComparison.Ordinal);
        Assert.Contains("[RegisterSingleton<ILogFileStorage>]", provider, StringComparison.Ordinal);
        Assert.Contains("opfs:/logs", provider, StringComparison.Ordinal);
    }

    private static string ReadBrowserScript(string repositoryRoot, string fileName) =>
        File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "wwwroot",
            fileName));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OngekiFumenEditor.Avalonia.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not find the Avalonia repository root.");
    }
}
