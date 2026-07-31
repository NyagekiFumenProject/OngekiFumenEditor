using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.FumenVisualEditor.ViewModels;

public partial class FumenVisualEditorGlobalSettingViewModel : ObservableObject
{
    public EditorGlobalSetting Setting => EditorGlobalSetting.Default;

    public FumenVisualEditorGlobalSettingViewModel()
    {
        EditorGlobalSetting.Default.PropertyChanged += (_, e) => Log.LogDebug($"editor global setting property changed : {e.PropertyName}");
    }

    public void ApplyChanges()
    {
        EditorGlobalSetting.Default.Save();
    }
}

