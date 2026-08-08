using Avalonia.Input;

namespace OngekiFumenEditor.Avalonia.Kernel.KeyBinding;

internal interface IKeyBindingManager
{
    bool CheckKeyBinding(KeyBindingDefinition defination, KeyEventArgs e);

    void ChangeKeyBinding(KeyBindingDefinition definition, Key newKey, KeyModifiers newModifier);

    void DefaultKeyBinding(KeyBindingDefinition definition) =>
        ChangeKeyBinding(definition, definition.DefaultKey, definition.DefaultModifiers);

    KeyBindingDefinition QueryKeyBinding(Key key, KeyModifiers modifier, KeyBindingLayer layer);

    IReadOnlyList<KeyBindingDefinition> QueryKeyBindingConflicts(
        KeyBindingDefinition definition,
        Key key,
        KeyModifiers modifier)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (key is Key.None)
            return [];

        return KeyBindingDefinations
            .Where(x => x != definition &&
                        x.Key == key &&
                        x.Modifiers == modifier &&
                        (x.Layer == KeyBindingLayer.Global ||
                         definition.Layer == KeyBindingLayer.Global ||
                         x.Layer == definition.Layer))
            .ToArray();
    }

    void ChangeKeyBindingResolvingConflicts(
        KeyBindingDefinition definition,
        Key newKey,
        KeyModifiers newModifier)
    {
        foreach (var conflict in QueryKeyBindingConflicts(definition, newKey, newModifier))
            ChangeKeyBinding(conflict, Key.None, KeyModifiers.None);

        ChangeKeyBinding(definition, newKey, newModifier);
    }

    void SaveConfig();

    void LoadConfig();

    IEnumerable<KeyBindingDefinition> KeyBindingDefinations { get; }
}
