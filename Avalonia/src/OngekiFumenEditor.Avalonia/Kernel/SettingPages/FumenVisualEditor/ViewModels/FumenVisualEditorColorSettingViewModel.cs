using Gekimini.Avalonia.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using Gekimini.Avalonia.Modules.Settings;
using Microsoft.Extensions.Logging;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.FumenVisualEditor.Models;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Platforms.Services.Window;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.UI.Dialogs.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;
using System.Reflection;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.FumenVisualEditor.ViewModels;

[RegisterSingleton<ISettingsEditor>]
public partial class FumenVisualEditorColorSettingViewModel : ViewModelBase, ISettingsEditor
{
    public EditorGlobalSetting Setting => EditorGlobalSetting.Default;

    public string SettingsPageName => Lang.VisualEditorLaneColorSettings;

    public string SettingsPagePath => Lang.TabDocument + "\\" + Lang.TabEditor;
    public ColorPropertyWrapper[] ColorsProperties { get; }

    private readonly ILogger<FumenVisualEditorColorSettingViewModel> logger;

    public FumenVisualEditorColorSettingViewModel(ILogger<FumenVisualEditorColorSettingViewModel> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        logger.LogInformation("SelectColorAsync triggered (colorProperty={Name}).", colorProperty?.Name);
        if (colorProperty is null)
            return Task.CompletedTask;

        var dialog = new CommonColorPickerViewModel(
            () => colorProperty.Color.ToMediaColor(),
            color => colorProperty.Color = color.ToDrawingColor(),
            Lang.NamedColorChangeTitle.Format(colorProperty.Name));

        return IoC.Get<IWindowManager>().ShowWindowAsync(dialog);
    }
}

