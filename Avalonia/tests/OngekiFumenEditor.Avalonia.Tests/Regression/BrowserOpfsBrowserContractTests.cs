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
        Assert.Contains("windowManager.FindExistingWindow(viewModel)", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("Application.Current", handler, StringComparison.Ordinal);
        Assert.Contains("existingWindow.Activate()", handler, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserOpfsBrowser_OpensSingleFilesInNewBrowserPages()
    {
        string repositoryRoot = FindRepositoryRoot();
        string contracts = ReadSource(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "Modules",
            "BrowserOpfsBrowser",
            "BrowserOpfsContracts.cs");
        string viewModel = ReadSource(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "Modules",
            "BrowserOpfsBrowser",
            "ViewModels",
            "BrowserOpfsBrowserViewModel.cs");
        string script = ReadBrowserScript(repositoryRoot, "opfsBrowser.js");

        Assert.Contains("bool OpenFilePreview(string relativePath)", contracts, StringComparison.Ordinal);
        Assert.Contains("service.OpenFilePreview(entry.RelativePath)", viewModel, StringComparison.Ordinal);
        Assert.Contains("export function openFilePreview(relativePath)", script, StringComparison.Ordinal);
        Assert.Contains("globalThis.open(\"\", \"_blank\")", script, StringComparison.Ordinal);
        Assert.Contains("void loadFilePreview(previewWindow, normalizedPath);", script, StringComparison.Ordinal);
        Assert.Contains("previewWindow.location.replace(objectUrl);", script, StringComparison.Ordinal);
        Assert.Contains("previewWindow.opener = null;", script, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserOpfsBrowser_AppendsItsMenuRegistrationsAndRegistersItsViewsInEveryBrowserBuild()
    {
        string repositoryRoot = FindRepositoryRoot();
        string browserProject = ReadSource(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "OngekiFumenEditor.Avalonia.Browser.csproj");
        string llvmBrowserProject = ReadSource(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "OngekiFumenEditor.Avalonia.Browser.LLVM.csproj");
        string browserApplication = ReadSource(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "OngekiFumenEditorBrowserApp.cs");
        string browserViewActivator = ReadSource(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "BrowserViewTypeCollectedActivator.cs");

        Assert.Contains("<InjectioDuplicateStrategy>Append</InjectioDuplicateStrategy>", browserProject, StringComparison.Ordinal);
        Assert.Contains("<CompilerVisibleProperty Include=\"InjectioDuplicateStrategy\" />", browserProject, StringComparison.Ordinal);
        Assert.Contains("<InjectioDuplicateStrategy>Append</InjectioDuplicateStrategy>", llvmBrowserProject, StringComparison.Ordinal);
        Assert.Contains("<CompilerVisibleProperty Include=\"InjectioDuplicateStrategy\" />", llvmBrowserProject, StringComparison.Ordinal);
        Assert.Contains(
            "serviceCollection.AddTypeCollectedActivator(BrowserViewTypeCollectedActivator.Default)",
            browserApplication,
            StringComparison.Ordinal);
        Assert.Contains("[CollectTypeForActivator(typeof(IView))]", browserViewActivator, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserHost_ExposesApplicationTitleAndFaviconThroughTheDocument()
    {
        string repositoryRoot = FindRepositoryRoot();
        string index = ReadSource(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "wwwroot",
            "index.html");
        string windowScript = ReadBrowserScript(repositoryRoot, "window.js");
        string interop = ReadSource(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "Utils",
            "Interops",
            "WindowInterop.cs");
        string platformMainWindow = ReadSource(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "Platforms",
            "Services",
            "MainWindow",
            "BrowserPlatformMainWindow.cs");

        Assert.Contains("<title>Ongeki Fumen Editor</title>", index, StringComparison.Ordinal);
        Assert.Contains(
            "<link rel=\"icon\" href=\"./favicon.ico\" type=\"image/x-icon\" data-browser-icon=\"true\" />",
            index,
            StringComparison.Ordinal);
        Assert.Contains("globalThis.WindowInterop.setTitle", interop, StringComparison.Ordinal);
        Assert.Contains("globalThis.WindowInterop.setIcon", interop, StringComparison.Ordinal);
        Assert.Contains("function setTitle(title)", windowScript, StringComparison.Ordinal);
        Assert.Contains("function setIcon(url)", windowScript, StringComparison.Ordinal);
        Assert.Contains("setTitle,", windowScript, StringComparison.Ordinal);
        Assert.Contains("setIcon,", windowScript, StringComparison.Ordinal);
        Assert.True(
            index.IndexOf("./window.js", StringComparison.Ordinal) <
            index.IndexOf("./main.js", StringComparison.Ordinal));
        Assert.Contains("WindowInterop.SetTitle(nextTitle)", platformMainWindow, StringComparison.Ordinal);
        Assert.Contains("WindowInterop.SetIcon(BrowserIconPath)", platformMainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "BrowserPlatformMainWindow not support get/set Title",
            platformMainWindow,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "BrowserPlatformMainWindow not support get/set Icon",
            platformMainWindow,
            StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "wwwroot",
            "favicon.ico")));
    }

    [Fact]
    public void BrowserKeyBindingManager_UsesSharedBrowserOpfsInteropAndIsInitializedBeforeRouting()
    {
        string repositoryRoot = FindRepositoryRoot();
        string manager = ReadSource(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "Platforms",
            "Services",
            "KeyBinding",
            "BrowserKeyBindingManager.cs");
        string interop = ReadSource(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "Utils",
            "Interops",
            "BrowserOpfsInterop.cs");
        string script = ReadBrowserScript(repositoryRoot, "opfsBrowser.js");
        string main = ReadBrowserScript(repositoryRoot, "main.js");
        string app = ReadSource(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia",
            "OngekiFumenEditorApp.cs");

        Assert.Contains("[RegisterSingleton<IKeyBindingManager>]", manager, StringComparison.Ordinal);
        Assert.Contains("ConfigFilePath = \"opfs:/keybind.json\"", manager, StringComparison.Ordinal);
        Assert.Contains(".ReadFileAsync(KeyBindingFileName)", manager, StringComparison.Ordinal);
        Assert.Contains(".WriteFileAsync(KeyBindingFileName, handle)", manager, StringComparison.Ordinal);
        Assert.Contains("namespace OngekiFumenEditor.Avalonia.Browser.Utils.Interops;", interop, StringComparison.Ordinal);
        Assert.Contains("globalThis.BrowserOpfsInterop.readFile", interop, StringComparison.Ordinal);
        Assert.Contains("globalThis.BrowserOpfsInterop.writeFile", interop, StringComparison.Ordinal);
        Assert.Contains("export async function readFile(relativePath)", script, StringComparison.Ordinal);
        Assert.Contains("return { data: null }", script, StringComparison.Ordinal);
        Assert.Contains("export async function writeFile(relativePath, handle)", script, StringComparison.Ordinal);
        Assert.Contains("readFile,", script, StringComparison.Ordinal);
        Assert.Contains("writeFile,", script, StringComparison.Ordinal);
        Assert.Contains("initializeBrowserOpfs(),", main, StringComparison.Ordinal);
        Assert.DoesNotContain("keyBindingFileSystem", main, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "Platforms",
            "Services",
            "FileSystem",
            "BrowserOpfs",
            "BrowserOpfsInterop.cs")));
        Assert.False(File.Exists(Path.Combine(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "wwwroot",
            "keyBindingFileSystem.js")));
        Assert.False(File.Exists(Path.Combine(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "Platforms",
            "Services",
            "KeyBinding",
            "BrowserKeyBindingFileSystemInterop.cs")));
        Assert.Contains("await ServiceProvider.GetRequiredService<IKeyBindingManager>().Initialize()", app, StringComparison.Ordinal);
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
