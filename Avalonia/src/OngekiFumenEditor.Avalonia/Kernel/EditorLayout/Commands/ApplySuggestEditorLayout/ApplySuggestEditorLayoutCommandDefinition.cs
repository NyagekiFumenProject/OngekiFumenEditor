using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Kernel.EditorLayout.Commands.ApplySuggestEditorLayout;

[RegisterSingleton<CommandDefinitionBase>]
public class ApplySuggestEditorLayoutCommandDefinition : CommandDefinition
{
    public const string CommandName = "EditorLayout.ApplySuggestEditorLayout";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = Lang.ApplySuggestedEditorLayout.ToLocalizedStringByRawText();

    public override LocalizedString ToolTip => Text;
}

