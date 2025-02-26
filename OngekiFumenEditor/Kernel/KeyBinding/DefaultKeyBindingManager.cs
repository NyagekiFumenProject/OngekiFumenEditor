using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;

namespace OngekiFumenEditor.Kernel.KeyBinding
{
    [Export(typeof(IKeyBindingManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class DefaultKeyBindingManager : IKeyBindingManager
    {
        private readonly string jsonConfigFilePath;

        private class Config
        {
            public Dictionary<string, string> KeyBindings { get; set; } = new();
        }

        public IEnumerable<KeyBindingDefinition> KeyBindingDefinations => definitionMap.Values;

        private Dictionary<string, KeyBindingDefinition> definitionMap = new();

        private JsonSerializerOptions serializerOptions = new JsonSerializerOptions()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        [ImportingConstructor]
        public DefaultKeyBindingManager([ImportMany] KeyBindingDefinition[] definations)
        {
            definitionMap = definations.ToDictionary(x => x.ConfigKey, x => x);

            jsonConfigFilePath = Path.GetFullPath("./keybind.json");
            Log.LogInfo($"jsonConfigFilePath: {jsonConfigFilePath}");

            LoadConfig();
        }

        public void SaveConfig()
        {
            var json = JsonSerializer.Serialize(new Config() { KeyBindings = definitionMap.ToDictionary(x => x.Key, x => KeyBindingDefinition.FormatToExpression(x.Value.Key, x.Value.Modifiers)) }, serializerOptions);
            File.WriteAllText(jsonConfigFilePath, json);

            Log.LogInfo($"Saved.");
        }

        public void LoadConfig()
        {
            if (File.Exists(jsonConfigFilePath))
            {
                try
                {
                    var json = File.ReadAllText(jsonConfigFilePath);
                    var strMap = JsonSerializer.Deserialize<Config>(json).KeyBindings;

                    foreach (var item in strMap)
                    {
                        var name = item.Key;
                        var expr = item.Value;

                        if (!KeyBindingDefinition.TryParseExpression(expr, out var k, out var m))
                        {
                            Log.LogError($"Can't parse {name} keybinding expr: {expr}");
                            continue;
                        }

                        if (definitionMap.TryGetValue(name, out var definition))
                        {
                            definition.Key = k;
                            definition.Modifiers = m;
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.LogInfo($"Load failed: {e.Message}");
                }
            }

            Log.LogInfo($"Loaded.");
        }

        public bool CheckKeyBinding(KeyBindingDefinition defination, KeyEventArgs e)
        {
            var key = (e.Key == Key.System) ? e.SystemKey : e.Key;

            if (defination.Key == Key.None)
                return false;

            var modifier = Keyboard.Modifiers;

            return (key == defination.Key) && (modifier == GetActualModifiers(e.Key, defination.Modifiers));
        }

        private static ModifierKeys GetActualModifiers(Key key, ModifierKeys modifiers)
        {
            switch (key)
            {
                case Key.LeftCtrl:
                case Key.RightCtrl:
                    modifiers |= ModifierKeys.Control;
                    return modifiers;

                case Key.LeftAlt:
                case Key.RightAlt:
                    modifiers |= ModifierKeys.Alt;
                    return modifiers;

                case Key.LeftShift:
                case Key.RightShift:
                    modifiers |= ModifierKeys.Shift;
                    break;
            }

            return modifiers;
        }

        public void ChangeKeyBinding(KeyBindingDefinition definition, Key newKey, ModifierKeys newModifier)
        {
            Log.LogInfo($"[{definition.DisplayName}] {KeyBindingDefinition.FormatToExpression(definition.Key, definition.Modifiers)}  -->  {KeyBindingDefinition.FormatToExpression(newKey, newModifier)}");

            definition.Key = newKey;
            definition.Modifiers = newModifier;
        }

        public KeyBindingDefinition QueryKeyBinding(Key key, ModifierKeys modifier, KeyBindingLayer layer)
        {
            if (key is Key.None)
                return default;

            return KeyBindingDefinations.FirstOrDefault(
                x => x.Key == key && modifier == x.Modifiers && (x.Layer == KeyBindingLayer.Global || layer == KeyBindingLayer.Global || x.Layer == layer));
        }
    }
}
