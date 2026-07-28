using Avalonia.Input;

namespace OngekiFumenEditor.Avalonia.Kernel.KeyBinding;

internal interface IKeyBindingManager
{
    bool CheckKeyBinding(KeyBindingDefinition defination, KeyEventArgs e);

    void ChangeKeyBinding(KeyBindingDefinition definition, Key newKey, KeyModifiers newModifier);

    void DefaultKeyBinding(KeyBindingDefinition definition) =>
        ChangeKeyBinding(definition, definition.DefaultKey, definition.DefaultModifiers);

    KeyBindingDefinition QueryKeyBinding(Key key, KeyModifiers modifier, KeyBindingLayer layer);

    void SaveConfig();

    void LoadConfig();

    IEnumerable<KeyBindingDefinition> KeyBindingDefinations { get; }
}
