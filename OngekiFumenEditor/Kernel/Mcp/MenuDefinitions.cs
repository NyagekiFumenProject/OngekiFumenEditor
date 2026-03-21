using Gemini.Framework.Menus;
using OngekiFumenEditor.Kernel.Mcp.Commands;
using OngekiFumenEditor.Properties;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Kernel.Mcp
{
    public static class MenuDefinitions
    {
        [Export]
        public static MenuDefinition McpMenu = new MenuDefinition(Gemini.Modules.MainMenu.MenuDefinitions.MainMenuBar, 99998, Resources.McpMenuTitle);

        [Export]
        public static ExcludeMenuDefinition ExcludeMcpMenu = new ExcludeMenuDefinition(ProgramSetting.Default.EnableMcpServerInGUIMode ? null : McpMenu);

        [Export]
        public static MenuItemGroupDefinition McpInfoMenuGroup = new MenuItemGroupDefinition(McpMenu, 0);

        [Export]
        public static MenuItemGroupDefinition McpClientsMenuGroup = new MenuItemGroupDefinition(McpMenu, 1);

        [Export]
        public static MenuItemGroupDefinition McpControlMenuGroup = new MenuItemGroupDefinition(McpMenu, 2);

        [Export]
        public static MenuItemDefinition McpServerUrlMenuItem = new CommandMenuItemDefinition<McpServerUrlCommandDefinition>(McpInfoMenuGroup, 0);

        [Export]
        public static MenuItemDefinition ConnectedMcpClientsMenuItem = new CommandMenuItemDefinition<ConnectedMcpClientsCommandDefinition>(McpClientsMenuGroup, 0);

        [Export]
        public static MenuItemGroupDefinition ConnectedMcpClientsCascadeGroup = new MenuItemGroupDefinition(ConnectedMcpClientsMenuItem, 0);

        [Export]
        public static MenuItemDefinition ConnectedMcpClientsMenuItemList = new CommandMenuItemDefinition<RevokeMcpClientAuthorizationCommandListDefinition>(ConnectedMcpClientsCascadeGroup, 0);

        [Export]
        public static MenuItemDefinition StartMcpServerMenuItem = new CommandMenuItemDefinition<StartMcpServerCommandDefinition>(McpControlMenuGroup, 0);

        [Export]
        public static MenuItemDefinition StopMcpServerMenuItem = new CommandMenuItemDefinition<StopMcpServerCommandDefinition>(McpControlMenuGroup, 1);
    }
}
