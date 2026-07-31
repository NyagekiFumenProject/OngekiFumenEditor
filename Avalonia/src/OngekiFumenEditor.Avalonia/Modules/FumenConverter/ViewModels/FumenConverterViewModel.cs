using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.Tools;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenConverter.ViewModels;

[RegisterSingleton<IFumenConverterWindow>]
public class FumenConverterViewModel : ToolViewModelBase, IFumenConverterWindow
{
    public FumenConverterViewModel() : base("Fumen Converter".ToLocalizedStringByRawText())
    {
        Dock = global::Dock.Model.Core.DockMode.Bottom;
    }
}