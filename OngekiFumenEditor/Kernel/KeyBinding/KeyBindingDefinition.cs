using Caliburn.Micro;
using OngekiFumenEditor.Properties;
using System.Text.RegularExpressions;
using System;
using System.Windows.Input;

namespace OngekiFumenEditor.Kernel.KeyBinding
{
    public class KeyBindingDefinition : PropertyChangedBase
    {
        private readonly string resourceName;

        public Key DefaultKey { get; }
        public ModifierKeys DefaultModifiers { get; }
        public KeyBindingLayer Layer { get; }

        public string ConfigKey => resourceName;

        public string Name => Resources.ResourceManager.GetString(resourceName);

        public string DisplayName => $"[{Resources.ResourceManager.GetString($"kbd_layer_{Layer}")}]{Name}";

        public KeyBindingDefinition(string resourceName, Key defaultKey, KeyBindingLayer layer = KeyBindingLayer.Normal) : this(resourceName, ModifierKeys.None, defaultKey, layer)
        { }

        public KeyBindingDefinition(string resourceName, ModifierKeys defaultModifiers, Key defaultKey, KeyBindingLayer layer = KeyBindingLayer.Normal)
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
            set
            {
                Set(ref key, value);
            }
        }

        private ModifierKeys? modifiers;
        public ModifierKeys Modifiers
        {
            get => modifiers ?? DefaultModifiers;
            set
            {
                Set(ref modifiers, value);
            }
        }

        public static string FormatToExpression(Key key, ModifierKeys modifier)
        {
            var modifierStr = modifier switch
            {
                ModifierKeys.Alt => "Alt",
                ModifierKeys.Control => "Ctrl",
                ModifierKeys.Shift => "Shift",
                ModifierKeys.Windows => "Win",
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

        //Ctrl + A
        static Regex regex = new Regex(@"(\s*\w+\s*\+\s*)?(\w+)");

        public static bool TryParseExpression(string keybindExpr, out Key key, out ModifierKeys modifier)
        {
            key = Key.None;
            modifier = ModifierKeys.None;

            if (string.IsNullOrWhiteSpace(keybindExpr))
                return true;

            var match = regex.Match(keybindExpr);
            if (!match.Success)
                return false;

            var modifierStr = match.Groups[1].Value.Trim().ToLower().TrimEnd('+').Trim();
            if (!string.IsNullOrWhiteSpace(modifierStr))
            {
                modifier = modifierStr switch
                {
                    "ctrl" or "control" => ModifierKeys.Control,
                    "win" or "windows" => ModifierKeys.Windows,
                    "alt" => ModifierKeys.Alt,
                    "shift" => ModifierKeys.Shift,
                    _ => ModifierKeys.None
                };

                if (modifier == ModifierKeys.None)
                    return false;
            }

            var keyStr = match.Groups[2].Value.Trim();
            if (!Enum.TryParse<Key>(keyStr, true, out var k))
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
}