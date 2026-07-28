using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.Tools;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenTimeSignatureListViewer.ViewModels;

[RegisterSingleton<IFumenTimeSignatureListViewer>]
public class FumenTimeSignatureListViewerViewModel : ToolViewModelBase, IFumenTimeSignatureListViewer
{
    public FumenTimeSignatureListViewerViewModel() : base("Time Signatures".ToLocalizedStringByRawText())
    {
        Dock = Dock.Model.Core.DockMode.Bottom;
    }
}