using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Modules.InternalTest.ViewModels.Documents;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Modules.Toolbox.Models;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.InternalTest.ToolboxItems;

[RegisterSingleton<ToolboxItem>]
public class AddItem : ToolboxItem<InternalTestDocumentViewModel>
{
    public override LocalizedString Category => "Normal".ToLocalizedStringByRawText();
    public override LocalizedString Name => Lang.B.IncrementValue.ToLocalizedString();
    public override Uri IconSource => default;
}