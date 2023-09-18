using Gemini.Framework.Commands;
using Gemini.Framework.ToolBars;
using Gemini.Modules.UndoRedo.Commands;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands.BrushModeSwitch;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands.ShowCurveControlAlways;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Toolbars
{
    public static class ToolBarDefinitions
    {
        [Export]
        public static ToolBarDefinition EditorToolBar = new ToolBarDefinition(8, "Editor");

        [Export]
        public static ToolBarItemGroupDefinition EditorStatusToolBarGroup = new ToolBarItemGroupDefinition(EditorToolBar, 0);

        [Export]
        public static ToolBarItemDefinition BrushModeSwitchToolBarItem = new CommandToolBarItemDefinition<BrushModeSwitchCommandDefinition>(
            EditorStatusToolBarGroup, 0);

        [Export]
        public static ToolBarItemDefinition ShowCurveControlAlwaysToolBarItem = new CommandToolBarItemDefinition<ShowCurveControlAlwaysCommandDefinition>(
            EditorStatusToolBarGroup, 1);
    }
}
