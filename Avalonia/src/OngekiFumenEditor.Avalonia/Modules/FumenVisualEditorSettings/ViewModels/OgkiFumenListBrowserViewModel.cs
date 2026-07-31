using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.Tools;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditorSettings.ViewModels;

[RegisterSingleton<IFumenVisualEditorSettings>]
public class OgkiFumenListBrowserViewModel : ToolViewModelBase, IFumenVisualEditorSettings
{
    public OgkiFumenListBrowserViewModel() : base("Editor Settings".ToLocalizedStringByRawText())
    {
        Dock = global::Dock.Model.Core.DockMode.Right;
    }
}