using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.Tools;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.FumenBulletPalleteListViewer.ViewModels;

[RegisterSingleton<IFumenBulletPalleteListViewer>]
public class FumenBulletPalleteListViewerViewModel : ToolViewModelBase, IFumenBulletPalleteListViewer
{
    public FumenBulletPalleteListViewerViewModel() : base(Lang.FumenBulletPalleteListViewer.ToLocalizedStringByRawText())
    {
        Dock = Dock.Model.Core.DockMode.Bottom;
    }
}

