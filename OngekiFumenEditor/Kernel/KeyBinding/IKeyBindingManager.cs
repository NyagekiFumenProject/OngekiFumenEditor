using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace OngekiFumenEditor.Kernel.KeyBinding
{
    internal interface IKeyBindingManager
    {
        bool CheckKeyBinding(KeyBindingDefinition defination, KeyEventArgs e);

        void ChangeKeyBinding(KeyBindingDefinition definition, Key newKey, ModifierKeys newModifier);

        void DefaultKeyBinding(KeyBindingDefinition definition) =>
            ChangeKeyBinding(definition, definition.DefaultKey, definition.DefaultModifiers);

        KeyBindingDefinition QueryKeyBinding(Key key, ModifierKeys modifier, KeyBindingLayer layer);

        void SaveConfig();

        void LoadConfig();

        IEnumerable<KeyBindingDefinition> KeyBindingDefinations { get; }
    }
}
