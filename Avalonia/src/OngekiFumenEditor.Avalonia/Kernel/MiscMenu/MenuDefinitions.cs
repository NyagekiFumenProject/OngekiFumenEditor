using Gekimini.Avalonia.Framework.Menus;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Kernel.MiscMenu.Commands.About;
using OngekiFumenEditor.Avalonia.Kernel.MiscMenu.Commands.CallFullGC;
using OngekiFumenEditor.Avalonia.Kernel.MiscMenu.Commands.OpenUrlCommon;

namespace OngekiFumenEditor.Avalonia.Kernel.MiscMenu;

public static class MenuDefinitions
{
    [RegisterStaticObject]
    public static MenuItemGroupDefinition ProgramMiscOpenMenuGroup =
        new(Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.FileMenu, 8);

    [RegisterStaticObject]
    public static MenuItemDefinition CallFullGCMenuItem =
        new CommandMenuItemDefinition<CallFullGCCommandDefinition>(ProgramMiscOpenMenuGroup, 0);

    [RegisterStaticObject]
    public static MenuItemGroupDefinition HelpMenuGroup =
        new(Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.HelpMenu, 0);

    [RegisterStaticObject]
    public static MenuItemGroupDefinition AboutMenuGroup =
        new(Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.HelpMenu, 1);

    [RegisterStaticObject]
    public static MenuItemDefinition OpenProjectUrlMenuItem =
        new CommandMenuItemDefinition<OpenProjectUrlCommandDefinition>(HelpMenuGroup, 0);

    [RegisterStaticObject]
    public static MenuItemDefinition RequestIssueHelpMenuItem =
        new CommandMenuItemDefinition<RequestIssueHelpCommandDefinition>(HelpMenuGroup, 1);

    [RegisterStaticObject]
    public static MenuItemDefinition PostSuggestUrlMenuItem =
        new CommandMenuItemDefinition<PostSuggestCommandDefinition>(HelpMenuGroup, 2);

    [RegisterStaticObject]
    public static MenuItemDefinition UsageWikiMenuItem =
        new CommandMenuItemDefinition<UsageWikiCommandDefinition>(HelpMenuGroup, 3);

    [RegisterStaticObject]
    public static MenuItemDefinition AboutMenuItem =
        new CommandMenuItemDefinition<AboutCommandDefinition>(AboutMenuGroup, 4);
}

