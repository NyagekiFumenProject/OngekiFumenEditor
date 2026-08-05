using Gekimini.Avalonia.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.FumenVisualEditor.Models;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Platforms.Services.Window;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.UI.Dialogs.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;
using System.Reflection;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.FumenVisualEditor.ViewModels;

public partial class FumenVisualEditorColorSettingViewModel : ViewModelBase
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

    [RelayCommand]
    private Task SelectColorAsync(ColorPropertyWrapper colorProperty)
    {
        if (colorProperty is null)
            return Task.CompletedTask;

        var dialog = new CommonColorPickerViewModel(
            () => colorProperty.Color.ToMediaColor(),
            color => colorProperty.Color = color.ToDrawingColor(),
            Lang.NamedColorChangeTitle.Format(colorProperty.Name));

        return IoC.Get<IWindowManager>().ShowWindowAsync(dialog);
    }
}

