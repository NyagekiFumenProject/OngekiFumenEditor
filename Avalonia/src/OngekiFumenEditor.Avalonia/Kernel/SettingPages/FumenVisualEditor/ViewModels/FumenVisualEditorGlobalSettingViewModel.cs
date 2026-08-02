using Gekimini.Avalonia.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Platforms.Services.Window;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.UI.Dialogs;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.FumenVisualEditor.ViewModels;

public partial class FumenVisualEditorGlobalSettingViewModel : ViewModelBase
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

    [RelayCommand]
    private Task SelectBackgroundColorAsync() => ShowColorPickerAsync(
        () => System.Drawing.Color.FromArgb(Setting.PlayFieldBackgroundColor).ToMediaColor(),
        color => Setting.PlayFieldBackgroundColor = color.ToDrawingColor().ToArgb(),
        Lang.NamedColorChangeTitle.Format(Lang.BackgroundColor));

    [RelayCommand]
    private Task SelectForegroundColorAsync() => ShowColorPickerAsync(
        () => System.Drawing.Color.FromArgb(Setting.PlayFieldForegroundColor).ToMediaColor(),
        color => Setting.PlayFieldForegroundColor = color.ToDrawingColor().ToArgb(),
        Lang.NamedColorChangeTitle.Format(Lang.PlayFieldForegroundColor));

    private Task ShowColorPickerAsync(
        Func<global::Avalonia.Media.Color> getter,
        Action<global::Avalonia.Media.Color> setter,
        string title)
    {
        return IoC.Get<IWindowManager>().ShowWindowAsync(new CommonColorPicker(getter, setter, title));
    }
}

