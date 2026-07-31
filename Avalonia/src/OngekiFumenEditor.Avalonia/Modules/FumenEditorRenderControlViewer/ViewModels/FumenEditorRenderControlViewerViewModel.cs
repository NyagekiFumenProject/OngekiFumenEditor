using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.Tools;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.FumenEditorRenderControlViewer.ViewModels;

[RegisterSingleton<IFumenEditorRenderControlViewer>]
public class FumenEditorRenderControlViewerViewModel : ToolViewModelBase, IFumenEditorRenderControlViewer
{
    public FumenEditorRenderControlViewerViewModel() : base(Lang.B.FumenEditorRenderControlViewer.ToLocalizedString())
    {
        Dock = global::Dock.Model.Core.DockMode.Right;
    }
}

