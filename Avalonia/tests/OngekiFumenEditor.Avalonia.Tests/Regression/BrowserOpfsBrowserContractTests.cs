#nullable enable

using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Regression;

public sealed class BrowserOpfsBrowserContractTests
{
    [Fact]
    public void BrowserOpfsBrowser_IsOwnedEntirelyByBrowserProject()
    {
        string repositoryRoot = FindRepositoryRoot();
        string sharedModulePath = Path.Combine(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia",
            "Modules",
            "BrowserOpfsBrowser");
        string browserModulePath = Path.Combine(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "Modules",
            "BrowserOpfsBrowser");

        Assert.False(Directory.Exists(sharedModulePath));
        Assert.True(File.Exists(Path.Combine(browserModulePath, "BrowserOpfsContracts.cs")));
        Assert.True(File.Exists(Path.Combine(browserModulePath, "BrowserOpfsDownloadPlanner.cs")));
        Assert.True(File.Exists(Path.Combine(
            browserModulePath,
            "Assets",
            "Languages",
            "BrowserOpfsLang.cs")));
        Assert.True(File.Exists(Path.Combine(browserModulePath, "ViewModels", "BrowserOpfsBrowserViewModel.cs")));
        Assert.True(File.Exists(Path.Combine(browserModulePath, "Views", "BrowserOpfsBrowserView.axaml")));
        Assert.True(File.Exists(Path.Combine(browserModulePath, "Commands", "BrowseBrowserOpfsCommandHandler.cs")));

        string contracts = File.ReadAllText(Path.Combine(browserModulePath, "BrowserOpfsContracts.cs"));
        string viewModel = File.ReadAllText(
            Path.Combine(browserModulePath, "ViewModels", "BrowserOpfsBrowserViewModel.cs"));
        string view = File.ReadAllText(
            Path.Combine(browserModulePath, "Views", "BrowserOpfsBrowserView.axaml"));
        Assert.Contains(
            "namespace OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser;",
            contracts,
            StringComparison.Ordinal);
        Assert.Contains(
            "namespace OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser.ViewModels;",
            viewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "x:Class=\"OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser.Views.BrowserOpfsBrowserView\"",
            view,
            StringComparison.Ordinal);

        string sharedLanguagesPath = Path.Combine(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia",
            "Assets",
            "Languages");
        foreach (string sharedLanguageFile in Directory.EnumerateFiles(sharedLanguagesPath, "ProgramLang*.json"))
        {
            Assert.DoesNotContain(
                "BrowserOpfs",
                File.ReadAllText(sharedLanguageFile),
                StringComparison.Ordinal);
        }

        string browserLanguageDirectory = Path.Combine(browserModulePath, "Assets", "Languages");
        Assert.Equal(3, Directory.EnumerateFiles(browserLanguageDirectory, "BrowserOpfsLang*.json").Count());
        Assert.All(
            Directory.EnumerateFiles(browserLanguageDirectory, "BrowserOpfsLang*.json"),
            languageFile => Assert.Contains(
                "BrowserOpfsWindowTitle",
                File.ReadAllText(languageFile),
                StringComparison.Ordinal));
    }

    [Fact]
    public void BrowserOpfsBrowser_UsesChunkedStreamingValidatedManifestAndStagingFallback()
    {
        string repositoryRoot = FindRepositoryRoot();
        string script = ReadBrowserScript(repositoryRoot, "opfsBrowser.js");
        string main = ReadBrowserScript(repositoryRoot, "main.js");
        string service = ReadSource(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "Platforms",
            "Services",
            "FileSystem",
            "BrowserOpfs",
            "BrowserOpfsService.cs");

        Assert.Contains("globalThis.showSaveFilePicker", script, StringComparison.Ordinal);
        Assert.Contains("state.file.slice(state.offset, end).arrayBuffer()", script, StringComparison.Ordinal);
        Assert.Contains("createWritable()", script, StringComparison.Ordinal);
        Assert.Contains("queueDownloadBuffer", script, StringComparison.Ordinal);
        Assert.Contains(".ongeki-opfs-downloads", script, StringComparison.Ordinal);
        Assert.Contains("removeEntry(stagingDirectoryName, { recursive: true })", script, StringComparison.Ordinal);
        Assert.Contains("StagingState.waitingAutomaticCleanup", script, StringComparison.Ordinal);
        Assert.Contains("validateManifest", script, StringComparison.Ordinal);
        Assert.Contains("initialize as initializeBrowserOpfs", main, StringComparison.Ordinal);
        Assert.Contains("initializeBrowserOpfs(),", main, StringComparison.Ordinal);
        Assert.Contains("ZipArchiveMode.Create", service, StringComparison.Ordinal);
        Assert.Contains("CompressionLevel.Fastest", service, StringComparison.Ordinal);
        Assert.Contains("BrowserOpfsInterop.ValidateManifestAsync", service, StringComparison.Ordinal);
        Assert.Contains("await output.CommitAsync();", service, StringComparison.Ordinal);

        int validateIndex = service.IndexOf("BrowserOpfsInterop.ValidateManifestAsync", StringComparison.Ordinal);
        int commitIndex = service.IndexOf("await output.CommitAsync();", StringComparison.Ordinal);
        Assert.True(validateIndex >= 0 && commitIndex > validateIndex);
    }

    [Fact]
    public void BrowserOpfsBrowser_PollsIncrementallyHasNoSearchAndDisablesUnavailableMenu()
    {
        string repositoryRoot = FindRepositoryRoot();
        string viewModel = ReadSource(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "Modules",
            "BrowserOpfsBrowser",
            "ViewModels",
            "BrowserOpfsBrowserViewModel.cs");
        string view = ReadSource(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "Modules",
            "BrowserOpfsBrowser",
            "Views",
            "BrowserOpfsBrowserView.axaml");
        string handler = ReadSource(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "Modules",
            "BrowserOpfsBrowser",
            "Commands",
            "BrowseBrowserOpfsCommandHandler.cs");

        Assert.Contains("TimeSpan.FromSeconds(5)", viewModel, StringComparison.Ordinal);
        Assert.Contains("Entries.Insert", viewModel, StringComparison.Ordinal);
        Assert.Contains("Entries.RemoveAt", viewModel, StringComparison.Ordinal);
        Assert.Contains("Entries.Move", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("Entries.Clear", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("<TextBox", view, StringComparison.Ordinal);
        Assert.Contains("command.Enabled = service.IsAvailable", handler, StringComparison.Ordinal);
        Assert.Contains("existingWindow.Activate()", handler, StringComparison.Ordinal);
    }

    private static string ReadBrowserScript(string repositoryRoot, string fileName) =>
        ReadSource(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "wwwroot",
            fileName);

    private static string ReadSource(string repositoryRoot, params string[] segments) =>
        File.ReadAllText(Path.Combine([repositoryRoot, .. segments]));

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
