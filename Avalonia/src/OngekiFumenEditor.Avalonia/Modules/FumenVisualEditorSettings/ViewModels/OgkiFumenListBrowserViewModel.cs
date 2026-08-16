using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.Tools;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;
using System.ComponentModel;
using static OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models.EditorSetting;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditorSettings.ViewModels;

[RegisterSingleton<IFumenVisualEditorSettings>]
public class FumenVisualEditorSettingsViewModel : ToolViewModelBase, IFumenVisualEditorSettings
{
    public double[] UnitCloseSizeValues { get; } =
    [
        1d,
        2,
        3,
        4,
        5,
        6,
        7,
        8,
        9,
        10,
        11,
        12,
    ];

    public string[] SupportTimeFormats { get; } =
    [
        nameof(TimeFormat.TGrid),
        nameof(TimeFormat.AudioTime)
    ];

    public FumenVisualEditorViewModel Editor
    {
        get => field;
        set
        {
            if (!SetProperty(ref field, value))
                return;

            Setting = Editor?.Setting;

            if (Editor is null)
                Title = Lang.B.FumenVisualEditorSettings.ToLocalizedString();
            else
                Title = LocalizedString.CreateFromTemplateFunc(() =>
                    $"{Lang.B.FumenVisualEditorSettings.Text} - {Editor.EditorContext.FileName}");
        }
    }

    public EditorSetting Setting
    {
        get => field;
        set => SetProperty(ref field, value);
    }

    public FumenVisualEditorSettingsViewModel() : base(Lang.B.FumenVisualEditorSettings.ToLocalizedString())
    {
        Dock = global::Dock.Model.Core.DockMode.Right;
        IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnActivateEditorChanged;
        Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
    }

    private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
    {
        Editor = @new;
        this.RegisterOrUnregisterPropertyChangeEvent(old, @new, OnEditorPropertyChanged);
    }

    private void OnEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FumenVisualEditorViewModel.Setting))
            Setting = Editor?.Setting;
    }
}
