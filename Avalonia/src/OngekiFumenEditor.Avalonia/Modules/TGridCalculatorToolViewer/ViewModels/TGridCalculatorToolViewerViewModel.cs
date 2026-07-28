using Dock.Model.Core;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.Tools;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenMetaInfoBrowser;

namespace OngekiFumenEditor.Avalonia.Modules.TGridCalculatorToolViewer.ViewModels;

[RegisterSingleton<IFumenMetaInfoBrowser>]
public class TGridCalculatorToolViewerViewModel : ToolViewModelBase, IFumenMetaInfoBrowser
{
    public TGridCalculatorToolViewerViewModel() : base("TGrid Calculator".ToLocalizedStringByRawText())
    {
        Dock = DockMode.Bottom;
    }
}