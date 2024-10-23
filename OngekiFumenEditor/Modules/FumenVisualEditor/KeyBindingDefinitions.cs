using OngekiFumenEditor.Kernel.KeyBinding;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenVisualEditor
{
    public static class KeyBindingDefinitions
    {
        [Export]
        public static KeyBindingDefinition KBD_FastSetObjectIsCritical = new KeyBindingDefinition(
            "kbd_editor_FastSetObjectIsCritical",
             Key.C);

        [Export]
        //KeyboardAction_FastPlaceDockableObjectToWallLeft
        public static KeyBindingDefinition KBD_FastPlaceDockableObjectToWallLeft = new KeyBindingDefinition(
            "kbd_editor_FastPlaceDockableObjectToWallLeft",
             Key.OemTilde);

        [Export]
        //KeyboardAction_FastPlaceDockableObjectToWallRight
        public static KeyBindingDefinition KBD_FastPlaceDockableObjectToWallRight = new KeyBindingDefinition(
            "kbd_editor_FastPlaceDockableObjectToWallRight",
             Key.D4);

        [Export]
        //KeyboardAction_FastPlaceDockableObjectToRight
        public static KeyBindingDefinition KBD_FastPlaceDockableObjectToRight = new KeyBindingDefinition(
            "kbd_editor_FastPlaceDockableObjectToRight",
             Key.D3);

        [Export]
        //[Key H] = [Action KeyboardAction_FastPlaceNewHold($executionContext)]
        public static KeyBindingDefinition KBD_FastPlaceNewHold = new KeyBindingDefinition(
            "kbd_editor_FastPlaceNewHold",
             Key.H);

        [Export]
        //[Key T] = [Action KeyboardAction_FastPlaceNewTap($executionContext)];
        public static KeyBindingDefinition KBD_FastPlaceNewTap = new KeyBindingDefinition(
            "kbd_editor_FastPlaceNewTap",
             Key.T);

        [Export]
        //[Key D2] = [Action KeyboardAction_FastPlaceDockableObjectToCenter];
        public static KeyBindingDefinition KBD_FastPlaceDockableObjectToCenter = new KeyBindingDefinition(
            "kbd_editor_FastPlaceDockableObjectToCenter",
             Key.D2);

        [Export]
        //[Key D1] = [Action KeyboardAction_FastPlaceDockableObjectToLeft];
        public static KeyBindingDefinition KBD_FastPlaceDockableObjectToLeft = new KeyBindingDefinition(
            "kbd_editor_FastPlaceDockableObjectToLeft",
             Key.D1);


        [Export]
        //[Key Delete] = [Action KeyboardAction_DeleteSelectingObjects]; 
        public static KeyBindingDefinition KBD_DeleteSelectingObjects = new KeyBindingDefinition(
            "kbd_editor_DeleteSelectingObjects",
             Key.Delete);

        [Export]
        //[Gesture Ctrl+A] = [Action KeyboardAction_SelectAllObjects];
        public static KeyBindingDefinition KBD_SelectAllObjects = new KeyBindingDefinition(
            "kbd_editor_SelectAllObjects",
            ModifierKeys.Control, Key.A);

        [Export]
        //[Key Escape] = [Action KeyboardAction_CancelSelectingObjects];
        public static KeyBindingDefinition KBD_CancelSelectingObjects = new KeyBindingDefinition(
            "kbd_editor_CancelSelectingObjects",
             Key.Escape);

        [Export]
        //[Key Q] = [Action KeyboardAction_HideOrShow];
        public static KeyBindingDefinition KBD_HideOrShow = new KeyBindingDefinition(
            "kbd_editor_HideOrShow",
             Key.Q);

        [Export]
        //        [Key A] = [Action KeyboardAction_FastAddConnectableChild($executionContext)]; 
        public static KeyBindingDefinition KBD_FastAddConnectableChild = new KeyBindingDefinition(
            "kbd_editor_FastAddConnectableChild",
             Key.A);

        [Export]
        //        [Key F] = [Action KeyboardAction_FastSwitchFlickDirection($executionContext)]; 
        public static KeyBindingDefinition KBD_FastSwitchFlickDirection = new KeyBindingDefinition(
            "kbd_editor_FastSwitchFlickDirection",
             Key.F);

        [Export]
        //        [Gesture Ctrl+C]=[Action MenuItemAction_CopySelectedObjects];
        public static KeyBindingDefinition KBD_CopySelectedObjects = new KeyBindingDefinition(
            "kbd_editor_CopySelectedObjects",
             ModifierKeys.Control, Key.C);

        [Export]
        //        [Gesture Ctrl+V]=[Action MenuItemAction_PasteCopiesObjects];    "
        public static KeyBindingDefinition KBD_PasteCopiesObjects = new KeyBindingDefinition(
            "kbd_editor_PasteCopiesObjects",
             ModifierKeys.Control, Key.V);

        [Export]
        //         [Key PageDown] = [Action ScrollPage(-1)]   "
        public static KeyBindingDefinition KBD_ScrollPageDown = new KeyBindingDefinition(
            "kbd_editor_ScrollPageDown",
             Key.PageDown);

        [Export]
        //        [Key PageUp] = [Action ScrollPage(1)];    "
        public static KeyBindingDefinition KBD_ScrollPageUp = new KeyBindingDefinition(
            "kbd_editor_ScrollPageUp",
             Key.PageUp);

    }
}
