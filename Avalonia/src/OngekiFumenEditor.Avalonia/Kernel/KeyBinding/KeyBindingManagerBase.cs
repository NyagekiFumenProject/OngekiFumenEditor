#nullable enable

using Avalonia.Input;
using System.Diagnostics;
using OngekiFumenEditor.Avalonia.Utils;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OngekiFumenEditor.Avalonia.Kernel.KeyBinding;

/// <summary>
/// Shared, persistence-free key binding behavior.
/// Platform projects provide the storage implementation and registration.
/// </summary>
public abstract class KeyBindingManagerBase : IKeyBindingManager
{
    private readonly Dictionary<string, KeyBindingDefinition> definitionMap;
    private readonly JsonSerializerOptions serializerOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    private readonly KeyBindingJsonSourceGenerationContext serializerContext;

    protected KeyBindingManagerBase(IEnumerable<KeyBindingDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        definitionMap = definitions.ToDictionary(x => x.ConfigKey, x => x);
        serializerContext = new KeyBindingJsonSourceGenerationContext(serializerOptions);
    }

    public IEnumerable<KeyBindingDefinition> KeyBindingDefinations => definitionMap.Values;

    public abstract Task Initialize();

    public bool CheckKeyBinding(KeyBindingDefinition defination, KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(defination);
        ArgumentNullException.ThrowIfNull(e);

        var key = e.Key;
        if (defination.Key == Key.None)
            return false;

        var modifier = e.KeyModifiers;
        return key == defination.Key && modifier == GetActualModifiers(e.Key, defination.Modifiers);
    }

    public void ChangeKeyBinding(KeyBindingDefinition definition, Key newKey, KeyModifiers newModifier)
    {
        ArgumentNullException.ThrowIfNull(definition);
        LogInfoSafe(
            $"[{definition.DisplayName}] {KeyBindingDefinition.FormatToExpression(definition.Key, definition.Modifiers)}  -->  {KeyBindingDefinition.FormatToExpression(newKey, newModifier)}");

        definition.Key = newKey;
        definition.Modifiers = newModifier;
    }

    public KeyBindingDefinition QueryKeyBinding(Key key, KeyModifiers modifier, KeyBindingLayer layer)
    {
        if (key is Key.None)
            return default!;

        return KeyBindingDefinations.FirstOrDefault(x =>
            x.Key == key &&
            modifier == x.Modifiers &&
            (x.Layer == KeyBindingLayer.Global || layer == KeyBindingLayer.Global || x.Layer == layer))!;
    }

    /// <summary>
    /// Serializes the current in-memory bindings for a platform store.
    /// </summary>
    protected string SerializeConfig()
    {
        return JsonSerializer.Serialize(
            new KeyBindingConfig
            {
                KeyBindings = definitionMap.ToDictionary(
                    x => x.Key,
                    x => KeyBindingDefinition.FormatToExpression(x.Value.Key, x.Value.Modifiers))
            },
            serializerContext.KeyBindingConfig);
    }

    /// <summary>
    /// Applies a previously persisted JSON document. Invalid individual entries are ignored.
    /// </summary>
    protected void ApplyConfig(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            LogInfoSafe("Loaded key binding defaults.");
            return;
        }

        try
        {
            var strMap = JsonSerializer.Deserialize(json, serializerContext.KeyBindingConfig)?.KeyBindings ?? [];
            foreach (var item in strMap)
            {
                var name = item.Key;
                var expr = item.Value;
                if (!KeyBindingDefinition.TryParseExpression(expr, out var key, out var modifiers))
                {
                    LogErrorSafe($"Can't parse {name} keybinding expr: {expr}");
                    continue;
                }

                if (definitionMap.TryGetValue(name, out var definition))
                {
                    definition.Key = key;
                    definition.Modifiers = modifiers;
                }
            }
        }
        catch (Exception exception)
        {
            LogInfoSafe($"Load failed: {exception.Message}");
        }

        LogInfoSafe("Loaded.");
    }

    protected static void LogInfoSafe(string message)
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

    protected static void LogErrorSafe(string message, Exception? exception = null)
    {
        try
        {
            if (exception is null)
                Log.LogError(message);
            else
                Log.LogError(message, exception);
        }
        catch
        {
            Debug.WriteLine(exception is null ? message : $"{message}\n{exception}");
        }
    }

    private static KeyModifiers GetActualModifiers(Key key, KeyModifiers modifiers)
    {
        switch (key)
        {
            case Key.LeftCtrl:
            case Key.RightCtrl:
                modifiers |= KeyModifiers.Control;
                return modifiers;
            case Key.LeftAlt:
            case Key.RightAlt:
                modifiers |= KeyModifiers.Alt;
                return modifiers;
            case Key.LeftShift:
            case Key.RightShift:
                modifiers |= KeyModifiers.Shift;
                return modifiers;
            default:
                return modifiers;
        }
    }

    public abstract void SaveConfig();

    public abstract void LoadConfig();
}

internal sealed class KeyBindingConfig
{
    public Dictionary<string, string> KeyBindings { get; set; } = [];
}

[JsonSerializable(typeof(KeyBindingConfig))]
internal partial class KeyBindingJsonSourceGenerationContext : JsonSerializerContext
{
}
