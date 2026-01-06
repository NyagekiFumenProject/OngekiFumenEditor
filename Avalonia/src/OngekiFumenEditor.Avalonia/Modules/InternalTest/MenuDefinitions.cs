using OngekiFumenEditor.Avalonia.Modules.InternalTest.Commands;
using Gemini.Framework.Menus;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.InternalTest;

public static class MenuDefinitions
{
    [RegisterStaticObject]
    public static MenuItemDefinition ViewInternalTestTool =
        new CommandMenuItemDefinition<ViewInternalTestToolCommandDefinition>(
            Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 5);
}