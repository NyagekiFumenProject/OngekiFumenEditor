using Gemini.Framework.Menus;
using OngekiFumenEditor.Kernel.ProgramUpdater.Commands.About;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.ProgramUpdater
{
    public static class MenuDefinitions
    {
        private class IconMenuDefinition : MenuDefinition
        {
            private readonly Uri uri;

            public IconMenuDefinition(MenuBarDefinition menuBar, int sortOrder, string text, Uri uri) : base(menuBar, sortOrder, text)
            {
                this.uri = uri;
            }

            public override Uri IconSource => uri;
        }

        [Export]
        public static MenuBarDefinition dummyMenuBar = new MenuBarDefinition();
        [Export]
        public static MenuDefinition newVersionMenu
            = new IconMenuDefinition(dummyMenuBar, 0, "有新版本!",
                new Uri("pack://application:,,,/OngekiFumenEditor;component/Resources/Icons/notication.png"));
        [Export]
        public static MenuItemGroupDefinition newVersionGroup = new MenuItemGroupDefinition(newVersionMenu, 0);
        [Export]
        public static MenuItemDefinition newVersionMenuItem = new CommandMenuItemDefinition<ShowNewVersionCommandDefinition>(newVersionGroup, 0);
    }
}
