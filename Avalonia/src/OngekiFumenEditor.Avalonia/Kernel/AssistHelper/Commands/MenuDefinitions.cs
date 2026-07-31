using Gekimini.Avalonia.Framework.Menus;
using Gekimini.Avalonia.Framework.Menus;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Kernel.AssistHelper.Commands.AdjustDockablesHorizonPosition;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Kernel.AssistHelper.Commands;

public static class MenuDefinitions
{
    [RegisterStaticObject]
    public static MenuDefinition AssistMenu =
        new(Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.MainMenuBar, 7, Lang.B.MenuAssist.ToLocalizedString());

    [RegisterStaticObject]
    public static MenuItemGroupDefinition AssistMenuGroup = new(AssistMenu, 0);

    [RegisterStaticObject]
    public static MenuItemDefinition AdjustDockablesHorizonPositionMenuItem =
        new CommandMenuItemDefinition<AdjustDockablesHorizonPositionCommandDefinition>(AssistMenuGroup, 0);
}


