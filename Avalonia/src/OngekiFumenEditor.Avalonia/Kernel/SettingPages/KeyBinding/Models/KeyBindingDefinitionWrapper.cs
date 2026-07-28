using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Kernel.KeyBinding;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.KeyBinding.Models;

public partial class KeyBindingDefinitionWrapper : ObservableObject
{
    private readonly KeyBindingDefinition definition;

    public KeyBindingDefinitionWrapper(KeyBindingDefinition definition)
    {
        this.definition = definition;
    }

    public KeyBindingDefinition Definition => definition;
}
