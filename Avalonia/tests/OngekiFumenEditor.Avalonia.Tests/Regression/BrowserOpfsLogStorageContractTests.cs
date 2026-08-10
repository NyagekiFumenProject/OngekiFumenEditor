using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Regression;

public sealed class BrowserOpfsLogStorageContractTests
{
    [Fact]
    public void BrowserOpfsLogStorage_CreatesLogsFolderAtOriginRootInsteadOfTemporaryRoot()
    {
        string repositoryRoot = FindRepositoryRoot();
        string script = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "wwwroot",
            "temporaryFileSystem.js"));
        string provider = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "Platforms",
            "Services",
            "Logging",
            "BrowserLogFileStorage.cs"));

        Assert.Contains(
            "logRoot = await originRoot.getDirectoryHandle(\"logs\", { create: true });",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "temporaryRoot.getDirectoryHandle(\"logs\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains("globalThis.LogFileSystemInterop", script, StringComparison.Ordinal);
        Assert.Contains("isAvailable: isLogAvailable", script, StringComparison.Ordinal);
        Assert.Contains("tryCreateFile: tryCreateLogFile", script, StringComparison.Ordinal);
        Assert.Contains("appendFile: appendLogFile", script, StringComparison.Ordinal);
        Assert.Contains("const logWriteBuffers = new Map();", script, StringComparison.Ordinal);
        Assert.Contains("setWriteBuffer: setLogWriteBuffer", script, StringComparison.Ordinal);
        Assert.Contains("[RegisterSingleton<ILogFileStorage>]", provider, StringComparison.Ordinal);
        Assert.Contains("opfs:/logs", provider, StringComparison.Ordinal);
    }

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
