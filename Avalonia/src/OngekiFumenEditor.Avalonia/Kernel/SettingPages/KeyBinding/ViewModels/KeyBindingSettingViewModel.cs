using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Kernel.KeyBinding;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.KeyBinding.ViewModels;

public partial class KeyBindingSettingViewModel : ObservableObject
{
    private readonly IKeyBindingManager keybindingManager;
    private KeyBindingDefinition[] definitions;

    public List<KeyBindingDefinition> Definitions { get; } = [];

    [ObservableProperty]
    public partial bool IsShowNotAssignOnly { get; set; }

    [ObservableProperty]
    public partial string FilterKeywords { get; set; } = string.Empty;

    public KeyBindingSettingViewModel()
    {
        keybindingManager = IoC.Get<IKeyBindingManager>();
        definitions = keybindingManager.KeyBindingDefinations.OrderBy(x => x.DisplayName).ToArray();
        UpdateDisplayList();
    }

    public void UpdateDisplayList()
    {
        Definitions.Clear();
        var list = definitions.AsEnumerable();

        if (IsShowNotAssignOnly)
            list = list.Where(x => x.Key == Key.None);

        if (!string.IsNullOrWhiteSpace(FilterKeywords))
            list = list.Where(x => string.Join(" ", [x.Name, x.Key, x.Modifiers, x.ConfigKey]).Contains(FilterKeywords, StringComparison.OrdinalIgnoreCase));

        Definitions.AddRange(list);
    }

    public void ApplyChanges()
    {
        keybindingManager.SaveConfig();
    }

    public void ResetAllDefinitions()
    {
        foreach (var definition in Definitions)
            keybindingManager.DefaultKeyBinding(definition);
        UpdateDisplayList();
    }

    partial void OnIsShowNotAssignOnlyChanged(bool value)
    {
        UpdateDisplayList();
    }

    partial void OnFilterKeywordsChanged(string value)
    {
        UpdateDisplayList();
    }
}
