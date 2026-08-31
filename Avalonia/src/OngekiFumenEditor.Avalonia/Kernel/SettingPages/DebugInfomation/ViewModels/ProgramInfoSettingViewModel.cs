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
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.DebugInfomation;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.DebugInfomation.ViewModels;

[RegisterSingleton<ISettingsEditor>]
public partial class ProgramInfoSettingViewModel : ViewModelBase, ISettingsEditor
{
    private readonly IRenderManager renderManager;
    private readonly IThreadingDiagnostics threadingDiagnostics;
    private Control loadedView;
    private ProgramInfoSnapshot snapshot;

    public ProgramInfoSettingViewModel(
        IRenderManager renderManager = null,
        IThreadingDiagnostics threadingDiagnostics = null)
    {
        this.renderManager = renderManager;
        this.threadingDiagnostics = threadingDiagnostics ?? DefaultThreadingDiagnostics.Instance;
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

    public void ResetDefault()
    {
        // Runtime diagnostics do not own persisted settings.
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
        var threadingInfo = GetThreadingInfo();

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
            EditorFpsLimit: renderTimerInfo.FpsLimit,
            GraphicsBackend: GetGraphicsBackend(),
            AvaloniaRenderer: GetAvaloniaRenderer(),
            PlatformRenderInterface: GetPlatformRenderInterface(),
            RenderTimer: renderTimerInfo.Name,
            RuntimeBackgroundThreads: FormatCapability(SupportsRuntimeBackgroundThreads()),
            AvaloniaRenderLoopBackgroundThreads: renderTimerInfo.BackgroundThreads,
            CoopHeader: threadingInfo.CoopHeader,
            CoepHeader: threadingInfo.CoepHeader,
            SharedArrayBuffer: threadingInfo.SharedArrayBuffer,
            WasmEnableThreads: threadingInfo.WasmEnableThreads,
            MainThreadId: threadingInfo.MainThreadId,
            UIThreadId: threadingInfo.UIThreadId,
            RenderThreadId: threadingInfo.RenderThreadId,
            LastRefreshed: DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture));
    }

    private (
        string CoopHeader,
        string CoepHeader,
        string SharedArrayBuffer,
        string WasmEnableThreads,
        string MainThreadId,
        string UIThreadId,
        string RenderThreadId)
        GetThreadingInfo()
    {
        ThreadingDiagnosticsSnapshot capabilities;
        try
        {
            capabilities = threadingDiagnostics.GetSnapshot();
        }
        catch (Exception)
        {
            capabilities = ThreadingDiagnosticsSnapshot.Unavailable;
        }

        return (
            FormatEnabled(capabilities.CoopHeaderEnabled),
            FormatEnabled(capabilities.CoepHeaderEnabled),
            FormatSupported(capabilities.SharedArrayBufferSupported),
            FormatEnabled(capabilities.WasmEnableThreadsEnabled),
            FormatThreadId(capabilities.MainThreadId),
            FormatThreadId(capabilities.UIThreadId),
            FormatThreadId(capabilities.RenderThreadId));
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

    private static (string Name, string FpsLimit, string BackgroundThreads) GetRenderTimerInfo()
    {
        try
        {
            var renderTimer = AvaloniaLocator.Current.GetService<IRenderTimer>();
            if (renderTimer is null)
                return (Lang.Unavailable, Lang.Unavailable, Lang.Unavailable);

            return (
                GetTypeName(renderTimer),
                FormatRenderFpsLimit(renderTimer),
                FormatCapability(renderTimer.RunsInBackground));
        }
        catch (Exception)
        {
            return (Lang.Unavailable, Lang.Unavailable, Lang.Unavailable);
        }
    }

    private static string FormatRenderFpsLimit(IRenderTimer renderTimer)
    {
        // The Avalonia render timer is the framework-wide frame-rate source.
        // Editor and audio limits only throttle their own work.
        if (renderTimer is DefaultRenderTimer defaultRenderTimer)
        {
            return defaultRenderTimer.FramesPerSecond > 0
                ? defaultRenderTimer.FramesPerSecond.ToString(CultureInfo.InvariantCulture)
                : Lang.Unlimited;
        }

        // Timers without an exposed fixed cap (for example DXGI/DWM timers
        // driven by VSync/display commits) are implementation-controlled.
        return Lang.PlatformControlled;
    }

    private static bool SupportsRuntimeBackgroundThreads()
    {
        // The net10 reference assemblies used by this solution do not expose
        // Thread.IsThreadStartSupported. Browser runtimes are the known target
        // where the normal managed background-thread model is unavailable.
        return !OperatingSystem.IsBrowser();
    }

    private static string FormatCapability(bool supported) =>
        supported ? Lang.Supported : Lang.NotSupported;

    private static string FormatEnabled(bool? enabled) =>
        enabled switch
        {
            true => Lang.Enabled,
            false => Lang.Disabled,
            _ => Lang.Unavailable
        };

    private static string FormatSupported(bool? supported) =>
        supported switch
        {
            true => Lang.Supported,
            false => Lang.NotSupported,
            _ => Lang.Unavailable
        };

    private static string FormatThreadId(int? threadId) =>
        threadId is > 0
            ? threadId.Value.ToString(CultureInfo.InvariantCulture)
            : Lang.Unavailable;

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
        string CoopHeader,
        string CoepHeader,
        string SharedArrayBuffer,
        string WasmEnableThreads,
        string MainThreadId,
        string UIThreadId,
        string RenderThreadId,
        string LastRefreshed);
}
