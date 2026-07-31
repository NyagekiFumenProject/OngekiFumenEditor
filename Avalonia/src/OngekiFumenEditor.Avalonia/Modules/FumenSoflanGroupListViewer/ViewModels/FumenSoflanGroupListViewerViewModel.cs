using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.Tools;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base.EditorObjects;

namespace OngekiFumenEditor.Avalonia.Modules.FumenSoflanGroupListViewer.ViewModels;

[RegisterSingleton<IFumenSoflanGroupListViewer>]
public class FumenSoflanGroupListViewerViewModel : ToolViewModelBase, IFumenSoflanGroupListViewer
{
    public FumenSoflanGroupListViewerViewModel() : base("Soflan Groups".ToLocalizedStringByRawText())
    {
        Dock = global::Dock.Model.Core.DockMode.Bottom;
    }

    public SoflanGroupWrapItem CurrentSelectedSoflanGroupWrapItem => null;
    public SoflanGroupWrapItem CurrentSoflansDisplaySoflanGroupWrapItem => null;
}