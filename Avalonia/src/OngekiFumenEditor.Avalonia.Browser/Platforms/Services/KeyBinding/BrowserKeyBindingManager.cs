#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Browser.Utils.Interops;
using OngekiFumenEditor.Avalonia.Kernel.KeyBinding;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.KeyBinding;

/// <summary>
/// Browser key binding manager. Configuration is persisted as <c>keybind.json</c>
/// in the origin-private file system (OPFS) root.
/// </summary>
[SupportedOSPlatform("browser")]
[RegisterSingleton<IKeyBindingManager>]
public sealed class BrowserKeyBindingManager : KeyBindingManagerBase
{
    public const string KeyBindingFileName = "keybind.json";
    public const string ConfigFilePath = "opfs:/keybind.json";

    private static readonly SemaphoreSlim MutationGate = new(1, 1);
    private readonly object sync = new();
    private readonly Task initialization;
    private Task pendingOperation;

    public BrowserKeyBindingManager(IEnumerable<KeyBindingDefinition> definitions)
        : base(definitions)
    {
        initialization = LoadConfigCoreAsync();
        pendingOperation = initialization;
    }

    /// <summary>
    /// Initializes the manager and completes after the initial OPFS read has applied the persisted bindings.
    /// </summary>
    public override Task Initialize() => initialization;

    public override void SaveConfig()
    {
        string json;
        try
        {
            // Capture the UI state before the queued asynchronous write can resume elsewhere.
            json = SerializeConfig();
        }
        catch (Exception exception)
        {
            LogDiagnostic($"Failed to serialize browser key binding configuration: {exception.Message}");
            return;
        }

        lock (sync)
        {
            pendingOperation = SaveAfterAsync(pendingOperation, json);
        }
    }

    public override void LoadConfig()
    {
        lock (sync)
        {
            pendingOperation = LoadAfterAsync(pendingOperation);
        }
    }

    private async Task LoadAfterAsync(Task previousOperation)
    {
        await IgnoreFailureAsync(previousOperation);
        await LoadConfigCoreAsync();
    }

    private async Task SaveAfterAsync(Task previousOperation, string json)
    {
        await IgnoreFailureAsync(previousOperation);
        await SaveConfigCoreAsync(json);
    }

    private async Task LoadConfigCoreAsync()
    {
        try
        {
            if (!IsStorageAvailable())
            {
                LogDiagnostic("Browser OPFS is unavailable; using default key bindings.");
                return;
            }

            using JSObject result = await BrowserOpfsInterop
                .ReadFileAsync(KeyBindingFileName)
                ;
            byte[]? bytes = result.GetPropertyAsByteArray("data");
            if (bytes is null || bytes.Length == 0)
            {
                LogDiagnostic("No persisted browser key binding configuration was found; using defaults.");
                return;
            }

            ApplyConfig(Encoding.UTF8.GetString(bytes));
        }
        catch (Exception exception)
        {
            // A corrupt or unavailable browser store must not prevent the editor from starting.
            LogDiagnostic($"Failed to load browser key binding configuration: {exception.Message}");
            Debug.WriteLine(exception);
        }
    }

    private async Task SaveConfigCoreAsync(string json)
    {
        try
        {
            if (!IsStorageAvailable())
            {
                LogDiagnostic("Browser OPFS is unavailable; key binding configuration was not persisted.");
                return;
            }

            byte[] data = Encoding.UTF8.GetBytes(json);
            await MutationGate.WaitAsync();
            try
            {
                int handle = BrowserOpfsInterop.AllocateWriteBufferHandle();
                try
                {
                    BrowserOpfsInterop.SetWriteBuffer(handle, data, data.Length);
                    await BrowserOpfsInterop.WriteFileAsync(KeyBindingFileName, handle);
                }
                finally
                {
                    BrowserOpfsInterop.ReleaseWriteBuffer(handle);
                }
            }
            finally
            {
                MutationGate.Release();
            }

            LogDiagnostic("Saved browser key binding configuration.");
        }
        catch (Exception exception)
        {
            // Persistence is best effort; retaining the in-memory binding is preferable to a UI failure.
            LogDiagnostic($"Failed to save browser key binding configuration: {exception.Message}");
            Debug.WriteLine(exception);
        }
    }

    private static bool IsStorageAvailable()
    {
        try
        {
            return BrowserOpfsInterop.IsAvailable();
        }
        catch
        {
            return false;
        }
    }

    private static async Task IgnoreFailureAsync(Task operation)
    {
        try
        {
            await operation;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
        }
    }

    private static void LogDiagnostic(string message)
    {
        try
        {
            Log.LogInfo(message);
        }
        catch
        {
            Debug.WriteLine(message);
        }
    }
}
