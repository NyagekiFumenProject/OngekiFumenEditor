using Avalonia.Rendering;
using Avalonia.Threading;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.DebugInfomation;

/// <summary>
/// Provides platform-specific information about the prerequisites for browser
/// and WebAssembly threading.
/// </summary>
public interface IThreadingDiagnostics
{
    ThreadingDiagnosticsSnapshot GetSnapshot();
}

/// <summary>
/// The raw capability values used by the debug information page. A null value
/// means that the platform cannot expose that piece of information.
/// </summary>
public readonly record struct ThreadingDiagnosticsSnapshot(
    bool? CoopHeaderEnabled,
    bool? CoepHeaderEnabled,
    bool? SharedArrayBufferSupported,
    bool? WasmEnableThreadsEnabled,
    int? MainThreadId,
    int? UIThreadId,
    int? RenderThreadId)
{
    // Keep the conventional .NET acronym spelling available to callers that
    // use the shorter UI name internally.
    public int? UiThreadId => UIThreadId;

    public ThreadingDiagnosticsSnapshot(
        bool? CoopHeaderEnabled,
        bool? CoepHeaderEnabled,
        bool? SharedArrayBufferSupported,
        bool? WasmEnableThreadsEnabled)
        : this(
            CoopHeaderEnabled,
            CoepHeaderEnabled,
            SharedArrayBufferSupported,
            WasmEnableThreadsEnabled,
            null,
            null,
            null)
    {
    }

    public static ThreadingDiagnosticsSnapshot Unavailable { get; } =
        new(null, null, null, null, null, null, null);
}

/// <summary>
/// Captures the managed thread IDs used by the application and Avalonia.
/// </summary>
public static class ThreadingDiagnosticsRuntime
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(100);
    private static int mainThreadId;
    private static int renderThreadId;

    /// <summary>
    /// Records the first managed thread that enters the platform application.
    /// </summary>
    public static void CaptureMainThread()
    {
        var currentThreadId = Environment.CurrentManagedThreadId;
        if (currentThreadId > 0)
            Interlocked.CompareExchange(ref mainThreadId, currentThreadId, 0);
    }

    public static (int? MainThreadId, int? UIThreadId, int? RenderThreadId) GetThreadIds()
    {
        CaptureMainThread();

        var uiThreadId = GetUiThreadId();
        var renderTimer = GetRenderTimer();
        var currentRenderThreadId = GetRenderThreadId(renderTimer, uiThreadId);

        return (
            ToNullableThreadId(Volatile.Read(ref mainThreadId)),
            uiThreadId,
            currentRenderThreadId);
    }

    private static int? GetUiThreadId()
    {
        try
        {
            var dispatcher = Dispatcher.UIThread;
            if (dispatcher.CheckAccess())
                return ToNullableThreadId(Environment.CurrentManagedThreadId);

            var threadId = dispatcher.Invoke(
                static () => Environment.CurrentManagedThreadId,
                DispatcherPriority.Send,
                CancellationToken.None,
                ProbeTimeout);
            return ToNullableThreadId(threadId);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static IRenderTimer GetRenderTimer()
    {
        try
        {
            return AvaloniaLocator.Current.GetService<IRenderTimer>();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static int? GetRenderThreadId(IRenderTimer renderTimer, int? uiThreadId)
    {
        if (renderTimer is null)
            return null;

        if (!renderTimer.RunsInBackground)
            return uiThreadId ?? GetUiThreadId();

        var cachedThreadId = Volatile.Read(ref renderThreadId);
        if (cachedThreadId > 0)
            return cachedThreadId;

        // The public render-timer contract exposes the execution thread only
        // through Tick. Keep the one-time diagnostic probe bounded so opening
        // the settings page cannot stall indefinitely.
        var completion = new TaskCompletionSource<int>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Action<TimeSpan> onTick = _ =>
            completion.TrySetResult(Environment.CurrentManagedThreadId);

        try
        {
            renderTimer.Tick += onTick;
            if (!completion.Task.Wait(ProbeTimeout))
                return null;

            var capturedThreadId = completion.Task.GetAwaiter().GetResult();
            if (capturedThreadId <= 0)
                return null;

            Interlocked.CompareExchange(ref renderThreadId, capturedThreadId, 0);
            return Volatile.Read(ref renderThreadId);
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            try
            {
                renderTimer.Tick -= onTick;
            }
            catch (Exception)
            {
                // The timer may already be shutting down.
            }
        }
    }

    private static int? ToNullableThreadId(int threadId) =>
        threadId > 0 ? threadId : null;
}

/// <summary>
/// Fallback used by desktop and test hosts, where browser headers and
/// WebAssembly runtime flags do not exist.
/// </summary>
public sealed class DefaultThreadingDiagnostics : IThreadingDiagnostics
{
    public static DefaultThreadingDiagnostics Instance { get; } = new();

    private DefaultThreadingDiagnostics()
    {
    }

    public ThreadingDiagnosticsSnapshot GetSnapshot()
    {
        var threadIds = ThreadingDiagnosticsRuntime.GetThreadIds();
        return new ThreadingDiagnosticsSnapshot(
            CoopHeaderEnabled: null,
            CoepHeaderEnabled: null,
            SharedArrayBufferSupported: null,
            WasmEnableThreadsEnabled: null,
            MainThreadId: threadIds.MainThreadId,
            UIThreadId: threadIds.UIThreadId,
            RenderThreadId: threadIds.RenderThreadId);
    }
}
