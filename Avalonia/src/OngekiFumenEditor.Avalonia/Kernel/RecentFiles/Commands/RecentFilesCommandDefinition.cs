using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Kernel.RecentFiles.Commands;

[RegisterSingleton<CommandDefinitionBase>]
public class RecentFilesCommandDefinition : CommandDefinition
{
    public const string CommandName = "File.RecentFiles";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = Lang.CommandRecentFiles.ToLocalizedStringByRawText();

    public override LocalizedString ToolTip { get; } = string.Empty.ToLocalizedStringByRawText();
}

