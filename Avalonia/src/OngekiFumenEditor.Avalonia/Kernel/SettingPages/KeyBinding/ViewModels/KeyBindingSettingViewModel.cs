using Gekimini.Avalonia.ViewModels;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Modules.Settings;
using Gekimini.Avalonia.Platforms.Services.Window;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Kernel.KeyBinding;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.KeyBinding.Dialogs;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.KeyBinding.ViewModels;

[RegisterSingleton<ISettingsEditor>]
public partial class KeyBindingSettingViewModel : ViewModelBase, ISettingsEditor
{
    private readonly IKeyBindingManager keybindingManager;
    private KeyBindingDefinition[] definitions;

    public List<KeyBindingDefinition> Definitions { get; } = [];

    public string SettingsPageName => Lang.KeyMap;

    public string SettingsPagePath => Lang.TabDocument;

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

    [RelayCommand]
    public void UpdateDisplayList()
    {
        Log.LogInfo($"UpdateDisplayList triggered (filter={FilterKeywords}, showUnassignedOnly={IsShowNotAssignOnly}).");
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

    public void ResetDefault()
    {
        foreach (var definition in definitions)
            keybindingManager.DefaultKeyBinding(definition);

        keybindingManager.SaveConfig();
        UpdateDisplayList();
    }

    [RelayCommand]
    private async Task ChangeKeybindAsync(KeyBindingDefinition definition)
    {
        Log.LogInfo($"ChangeKeybindAsync triggered ({definition?.DisplayName}).");
        if (definition is null)
            return;

        await IoC.Get<IWindowManager>().ShowDialogAsync(new ConfigKeyBindingDialog(definition));
        UpdateDisplayList();
    }

    [RelayCommand]
    private async Task ResetAllDefinitionsAsync()
    {
        Log.LogInfo("ResetAllDefinitionsAsync triggered.");
        if (!await IoC.Get<IDialogManager>().ShowComfirmDialog(
                Lang.ComfirmResetAllKeybindingDefinitions,
                Lang.Warning))
            return;

        foreach (var definition in definitions)
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
