#nullable enable

using System.Text;
using System.Threading.Tasks;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Kernel.KeyBinding;

namespace OngekiFumenEditor.Avalonia.Desktop.Platforms.Services.KeyBinding;

/// <summary>
/// Desktop key binding manager. The portable configuration is stored beside the
/// executable so a copied editor keeps its bindings with the application.
/// </summary>
[RegisterSingleton<IKeyBindingManager>]
public sealed class DesktopKeyBindingManager : KeyBindingManagerBase
{
    public const string KeyBindingFileName = "keybind.json";
    private readonly string configFilePath;

    public DesktopKeyBindingManager(IEnumerable<KeyBindingDefinition> definitions)
        : this(definitions, DefaultConfigFilePath)
    {
    }

    internal DesktopKeyBindingManager(
        IEnumerable<KeyBindingDefinition> definitions,
        string configFilePath)
        : base(definitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configFilePath);
        this.configFilePath = Path.GetFullPath(configFilePath);
        LoadConfig();
    }

    public string ConfigFilePath => configFilePath;

    public static string DefaultConfigFilePath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, KeyBindingFileName));

    public override Task Initialize() => Task.CompletedTask;

    public override void SaveConfig()
    {
        try
        {
            File.WriteAllText(ConfigFilePath, SerializeConfig(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            LogInfoSafe($"Saved key binding configuration to '{ConfigFilePath}'.");
        }
        catch (Exception exception)
        {
            LogErrorSafe($"Failed to save key binding configuration to '{ConfigFilePath}'.", exception);
        }
    }

    public override void LoadConfig()
    {
        if (!File.Exists(ConfigFilePath))
        {
            LogInfoSafe($"Key binding configuration does not exist at '{ConfigFilePath}'; using defaults.");
            return;
        }

        try
        {
            ApplyConfig(File.ReadAllText(ConfigFilePath));
        }
        catch (Exception exception)
        {
            LogInfoSafe($"Failed to load key binding configuration from '{ConfigFilePath}': {exception.Message}");
        }
    }
}
