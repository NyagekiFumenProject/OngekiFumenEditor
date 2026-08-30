using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Rendering;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Modules.Settings;
using Gekimini.Avalonia.ViewModels;
using Gekimini.Avalonia.Views;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.DebugInfomation.ViewModels;

[RegisterSingleton<ISettingsEditor>]
public partial class ProgramInfoSettingViewModel : ViewModelBase, ISettingsEditor
{
    private readonly IRenderManager renderManager;
    private Control loadedView;
    private ProgramInfoSnapshot snapshot;

    public ProgramInfoSettingViewModel(IRenderManager renderManager = null)
    {
        this.renderManager = renderManager;
        Refresh();
    }

    public string SettingsPageName => Lang.ProgramInformation;

    public string SettingsPagePath => Lang.Debug;

    public ProgramInfoSnapshot Snapshot
    {
        get => snapshot;
        private set => SetProperty(ref snapshot, value);
    }

    public void ApplyChanges()
    {
        // This page only presents runtime state; there is nothing to persist.
    }

    public override void OnViewAfterLoaded(IView view)
    {
        base.OnViewAfterLoaded(view);
        loadedView = view as Control;
        Refresh();
    }

    public override void OnViewBeforeUnload(IView view)
    {
        if (ReferenceEquals(loadedView, view))
            loadedView = null;

        base.OnViewBeforeUnload(view);
    }

    [RelayCommand]
    private void Refresh()
    {
        Snapshot = CreateSnapshot();
    }

    private ProgramInfoSnapshot CreateSnapshot()
    {
        var applicationAssembly = typeof(ProgramInfoSettingViewModel).Assembly;
        var renderTimerInfo = GetRenderTimerInfo();

        return new ProgramInfoSnapshot(
            ApplicationVersion: FormatVersion(applicationAssembly.GetName().Version),
            ProductVersion: GetProductVersion(applicationAssembly),
            BuildConfiguration: GetAssemblyAttribute<AssemblyConfigurationAttribute>(applicationAssembly)?.Configuration
                                ?? Lang.Unavailable,
            BuildTime: FormatTimestamp(GetAssemblyMetadata(applicationAssembly, "BuildDateTime")),
            CommitHash: GetCommitHash(applicationAssembly),
            CommitDate: FormatTimestamp(GetAssemblyMetadata(applicationAssembly, "GitCommitDate")),
            OperatingSystem: RuntimeInformation.OSDescription,
            ProcessArchitecture: RuntimeInformation.ProcessArchitecture.ToString(),
            DotNetRuntime: RuntimeInformation.FrameworkDescription,
            AvaloniaVersion: FormatVersion(typeof(Application).Assembly.GetName().Version),
            LogicalProcessorCount: Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture),
            EditorFpsLimit: FormatFpsLimit(),
            GraphicsBackend: GetGraphicsBackend(),
            AvaloniaRenderer: GetAvaloniaRenderer(),
            PlatformRenderInterface: GetPlatformRenderInterface(),
            RenderTimer: renderTimerInfo.Name,
            RuntimeBackgroundThreads: FormatCapability(SupportsRuntimeBackgroundThreads()),
            AvaloniaRenderLoopBackgroundThreads: renderTimerInfo.BackgroundThreads,
            LastRefreshed: DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture));
    }

    private string GetGraphicsBackend()
    {
        try
        {
            return renderManager?.GetCurrentRenderManagerImpl()?.Name ?? Lang.Unavailable;
        }
        catch (Exception)
        {
            return Lang.Unavailable;
        }
    }

    private string GetAvaloniaRenderer()
    {
        if (loadedView is null)
            return Lang.Unavailable;

        try
        {
            var topLevel = TopLevel.GetTopLevel(loadedView);
            return topLevel is IRenderRoot renderRoot
                ? GetTypeName(renderRoot.Renderer)
                : Lang.Unavailable;
        }
        catch (Exception)
        {
            return Lang.Unavailable;
        }
    }

    private static string GetPlatformRenderInterface()
    {
        try
        {
            return GetTypeName(AvaloniaLocator.Current.GetService<IPlatformRenderInterface>());
        }
        catch (Exception)
        {
            return Lang.Unavailable;
        }
    }

    private static (string Name, string BackgroundThreads) GetRenderTimerInfo()
    {
        try
        {
            var renderTimer = AvaloniaLocator.Current.GetService<IRenderTimer>();
            if (renderTimer is null)
                return (Lang.Unavailable, Lang.Unavailable);

            return (GetTypeName(renderTimer), FormatCapability(renderTimer.RunsInBackground));
        }
        catch (Exception)
        {
            return (Lang.Unavailable, Lang.Unavailable);
        }
    }

    private static bool SupportsRuntimeBackgroundThreads()
    {
        // The net10 reference assemblies used by this solution do not expose
        // Thread.IsThreadStartSupported. Browser runtimes are the known target
        // where the normal managed background-thread model is unavailable.
        return !OperatingSystem.IsBrowser();
    }

    private static string FormatFpsLimit()
    {
        var limit = Models.Settings.EditorGlobalSetting.Default.LimitFPS;
        return limit > 0
            ? limit.ToString(CultureInfo.InvariantCulture)
            : Lang.Unlimited;
    }

    private static string FormatCapability(bool supported) =>
        supported ? Lang.Supported : Lang.NotSupported;

    private static string GetProductVersion(Assembly assembly)
    {
        return GetAssemblyAttribute<AssemblyInformationalVersionAttribute>(assembly)?.InformationalVersion
               ?? FormatVersion(assembly.GetName().Version);
    }

    private static string GetCommitHash(Assembly assembly)
    {
        var informationalVersion = GetAssemblyAttribute<AssemblyInformationalVersionAttribute>(assembly)
            ?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informationalVersion))
            return Lang.Unavailable;

        var metadataSeparator = informationalVersion.IndexOf('+');
        if (metadataSeparator < 0 || metadataSeparator == informationalVersion.Length - 1)
            return Lang.Unavailable;

        var commitPart = informationalVersion[(metadataSeparator + 1)..]
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(part => part.Length >= 7 && part.All(Uri.IsHexDigit));

        return string.IsNullOrEmpty(commitPart) ? Lang.Unavailable : commitPart[..7];
    }

    private static string GetTypeName(object value) => value?.GetType().FullName ?? Lang.Unavailable;

    private static string FormatVersion(Version value) => value?.ToString() ?? Lang.Unavailable;

    private static string GetAssemblyMetadata(Assembly assembly, string key) =>
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == key)
            ?.Value;

    private static string FormatTimestamp(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Lang.Unavailable;

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out var timestamp)
            ? timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
            : Lang.Unavailable;
    }

    private static TAttribute GetAssemblyAttribute<TAttribute>(Assembly assembly)
        where TAttribute : Attribute => assembly.GetCustomAttribute<TAttribute>();

    public sealed record ProgramInfoSnapshot(
        string ApplicationVersion,
        string ProductVersion,
        string BuildConfiguration,
        string BuildTime,
        string CommitHash,
        string CommitDate,
        string OperatingSystem,
        string ProcessArchitecture,
        string DotNetRuntime,
        string AvaloniaVersion,
        string LogicalProcessorCount,
        string EditorFpsLimit,
        string GraphicsBackend,
        string AvaloniaRenderer,
        string PlatformRenderInterface,
        string RenderTimer,
        string RuntimeBackgroundThreads,
        string AvaloniaRenderLoopBackgroundThreads,
        string LastRefreshed);
}
