using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using System.Reflection;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.FumenVisualEditor.ViewModels;

public partial class FumenVisualEditorColorSettingViewModel : ObservableObject
{
    public EditorGlobalSetting Setting => EditorGlobalSetting.Default;

    public ColorPropertyWrapper[] ColorsProperties { get; }

    public FumenVisualEditorColorSettingViewModel()
    {
        ColorsProperties = typeof(EditorGlobalSetting)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.Name.StartsWith("Color") && x.PropertyType == typeof(System.Drawing.Color))
            .Select(x => new ColorPropertyWrapper(x, EditorGlobalSetting.Default))
            .ToArray();
    }

    public void ApplyChanges()
    {
        EditorGlobalSetting.Default.Save();
    }
}

