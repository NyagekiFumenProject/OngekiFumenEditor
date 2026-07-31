using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using System.Text.RegularExpressions;

namespace OngekiFumenEditor.Avalonia.Kernel.KeyBinding;

public partial class KeyBindingDefinition : ObservableObject
{
    private readonly string resourceName;

    public Key DefaultKey { get; }
    public KeyModifiers DefaultModifiers { get; }
    public KeyBindingLayer Layer { get; }

    public string ConfigKey => resourceName;

    public string Name => Lang.LocalizerManager.GetLocalizedText(resourceName);

    public string DisplayName => $"[{Lang.LocalizerManager.GetLocalizedText($"kbd_layer_{Layer}")}]{Name}";

    public KeyBindingDefinition(string resourceName, Key defaultKey, KeyBindingLayer layer = KeyBindingLayer.Normal)
        : this(resourceName, KeyModifiers.None, defaultKey, layer)
    {
    }

    public KeyBindingDefinition(string resourceName, KeyModifiers defaultModifiers, Key defaultKey, KeyBindingLayer layer = KeyBindingLayer.Normal)
    {
        this.resourceName = resourceName;
        DefaultModifiers = defaultModifiers;
        DefaultKey = defaultKey;
        Layer = layer;
    }

    private Key? key;

    public Key Key
    {
        get => key ?? DefaultKey;
        set => SetProperty(ref key, value);
    }

    private KeyModifiers? modifiers;

    public KeyModifiers Modifiers
    {
        get => modifiers ?? DefaultModifiers;
        set => SetProperty(ref modifiers, value);
    }

    public static string FormatToExpression(Key key, KeyModifiers modifier)
    {
        var modifierStr = modifier switch
        {
            KeyModifiers.Alt => "Alt",
            KeyModifiers.Control => "Ctrl",
            KeyModifiers.Shift => "Shift",
            KeyModifiers.Meta => "Win",
            _ => string.Empty,
        };

        var expr = key is Key.None ? string.Empty : key.ToString();

        if (!string.IsNullOrWhiteSpace(modifierStr))
            expr = modifierStr + " + " + expr;

        return expr;
    }

    public static string FormatToExpression(KeyBindingDefinition definition)
    {
        return FormatToExpression(definition.Key, definition.Modifiers);
    }

    private static readonly Regex regex = new(@"(\s*\w+\s*\+\s*)?(\w+)");

    public static bool TryParseExpression(string keybindExpr, out Key key, out KeyModifiers modifier)
    {
        key = Key.None;
        modifier = KeyModifiers.None;

        if (string.IsNullOrWhiteSpace(keybindExpr))
            return true;

        var match = regex.Match(keybindExpr);
        if (!match.Success)
            return false;

        var modifierStr = match.Groups[1].Value.Trim().ToLowerInvariant().TrimEnd('+').Trim();
        if (!string.IsNullOrWhiteSpace(modifierStr))
        {
            modifier = modifierStr switch
            {
                "ctrl" or "control" => KeyModifiers.Control,
                "win" or "windows" => KeyModifiers.Meta,
                "alt" => KeyModifiers.Alt,
                "shift" => KeyModifiers.Shift,
                _ => KeyModifiers.None
            };

            if (modifier == KeyModifiers.None)
                return false;
        }

        var keyStr = match.Groups[2].Value.Trim();
        if (!Enum.TryParse(keyStr, true, out Key k))
            return false;

        key = k;
        return key != Key.None;
    }
}

public enum KeyBindingLayer
{
    Global,
    Normal,
    Batch
}

