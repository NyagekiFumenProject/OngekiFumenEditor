using OngekiFumenEditor.Avalonia.Modules.InternalTest.ViewModels.Documents;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Modules.Toolbox.Models;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.InternalTest.ToolboxItems;

[RegisterSingleton<ToolboxItem>]
public class MultiplyItem : ToolboxItem<InternalTestDocumentViewModel>
{
    public override LocalizedString Category => "High".ToLocalizedStringByRawText();
    public override LocalizedString Name => "Multiply Item".ToLocalizedStringByRawText();
    public override Uri IconSource => default;
}