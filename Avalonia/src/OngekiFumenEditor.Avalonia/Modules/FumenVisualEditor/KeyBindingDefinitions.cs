using OngekiFumenEditor.Avalonia.Kernel.KeyBinding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Input;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor
{
    public static class KeyBindingDefinitions
    {
        [RegisterStaticObject]
        public static KeyBindingDefinition KBD_ChangeDockableLaneType = new KeyBindingDefinition(
            "kbd_editor_ChangeDockableLaneType",
             Key.S);

        [RegisterStaticObject]
        public static KeyBindingDefinition KBD_FastSetObjectIsCritical = new KeyBindingDefinition(
            "kbd_editor_FastSetObjectIsCritical",
             Key.C);

        [RegisterStaticObject]
        //KeyboardAction_FastPlaceDockableObjectToWallLeft
        public static KeyBindingDefinition KBD_FastPlaceDockableObjectToWallLeft = new KeyBindingDefinition(
            "kbd_editor_FastPlaceDockableObjectToWallLeft",
             Key.OemTilde);

        [RegisterStaticObject]
        //KeyboardAction_FastPlaceDockableObjectToWallRight
        public static KeyBindingDefinition KBD_FastPlaceDockableObjectToWallRight = new KeyBindingDefinition(
            "kbd_editor_FastPlaceDockableObjectToWallRight",
             Key.D4);

        [RegisterStaticObject]
        //KeyboardAction_FastPlaceDockableObjectToRight
        public static KeyBindingDefinition KBD_FastPlaceDockableObjectToRight = new KeyBindingDefinition(
            "kbd_editor_FastPlaceDockableObjectToRight",
             Key.D3);

        [RegisterStaticObject]
        //[Key H] = [Action KeyboardAction_FastPlaceNewHold($executionContext)]
        public static KeyBindingDefinition KBD_FastPlaceNewHold = new KeyBindingDefinition(
            "kbd_editor_FastPlaceNewHold",
             Key.H);

        [RegisterStaticObject]
        //[Key T] = [Action KeyboardAction_FastPlaceNewTap($executionContext)];
        public static KeyBindingDefinition KBD_FastPlaceNewTap = new KeyBindingDefinition(
            "kbd_editor_FastPlaceNewTap",
             Key.T);

        [RegisterStaticObject]
        //[Key D2] = [Action KeyboardAction_FastPlaceDockableObjectToCenter];
        public static KeyBindingDefinition KBD_FastPlaceDockableObjectToCenter = new KeyBindingDefinition(
            "kbd_editor_FastPlaceDockableObjectToCenter",
             Key.D2);

        [RegisterStaticObject]
        //[Key D1] = [Action KeyboardAction_FastPlaceDockableObjectToLeft];
        public static KeyBindingDefinition KBD_FastPlaceDockableObjectToLeft = new KeyBindingDefinition(
            "kbd_editor_FastPlaceDockableObjectToLeft",
             Key.D1);


        [RegisterStaticObject]
        //[Key Delete] = [Action KeyboardAction_DeleteSelectingObjects]; 
        public static KeyBindingDefinition KBD_DeleteSelectingObjects = new KeyBindingDefinition(
            "kbd_editor_DeleteSelectingObjects",
             Key.Delete, layer: KeyBindingLayer.Global);

        [RegisterStaticObject]
        //[Gesture Ctrl+A] = [Action KeyboardAction_SelectAllObjects];
        public static KeyBindingDefinition KBD_SelectAllObjects = new KeyBindingDefinition(
            "kbd_editor_SelectAllObjects",
            KeyModifiers.Control, Key.A);

        [RegisterStaticObject]
        //[Key Escape] = [Action KeyboardAction_CancelSelectingObjects];
        public static KeyBindingDefinition KBD_CancelSelectingObjects = new KeyBindingDefinition(
            "kbd_editor_CancelSelectingObjects",
             Key.Escape);

        [RegisterStaticObject]
        //[Key Q] = [Action KeyboardAction_HideOrShow];
        public static KeyBindingDefinition KBD_HideOrShow = new KeyBindingDefinition(
            "kbd_editor_HideOrShow",
             Key.Q);

        [RegisterStaticObject] public static KeyBindingDefinition KBD_ToggleBatchMode = new KeyBindingDefinition(
            nameof(Lang.BatchModeToggle),
            KeyModifiers.Alt,
            Key.B,
            layer: KeyBindingLayer.Global);

        [RegisterStaticObject]
        //        [Key A] = [Action KeyboardAction_FastAddConnectableChild($executionContext)]; 
        public static KeyBindingDefinition KBD_FastAddConnectableChild = new KeyBindingDefinition(
            "kbd_editor_FastAddConnectableChild",
             Key.A);

        [RegisterStaticObject]
        //        [Key F] = [Action KeyboardAction_FastSwitchFlickDirection($executionContext)]; 
        public static KeyBindingDefinition KBD_FastSwitchFlickDirection = new KeyBindingDefinition(
            "kbd_editor_FastSwitchFlickDirection",
             Key.F);

        [RegisterStaticObject]
        //        [Gesture Ctrl+C]=[Action MenuItemAction_CopySelectedObjects];
        public static KeyBindingDefinition KBD_CopySelectedObjects = new KeyBindingDefinition(
            "kbd_editor_CopySelectedObjects",
             KeyModifiers.Control, Key.C,
            layer: KeyBindingLayer.Global);

        [RegisterStaticObject]
        //        [Gesture Ctrl+V]=[Action MenuItemAction_PasteCopiesObjects];    "
        public static KeyBindingDefinition KBD_PasteCopiesObjects = new KeyBindingDefinition(
            "kbd_editor_PasteCopiesObjects",
             KeyModifiers.Control, Key.V,
            layer: KeyBindingLayer.Global);

        [RegisterStaticObject]
        //         [Key PageDown] = [Action ScrollPage(-1)]   "
        public static KeyBindingDefinition KBD_ScrollPageDown = new KeyBindingDefinition(
            "kbd_editor_ScrollPageDown",
             Key.PageDown,
            layer: KeyBindingLayer.Global);

        [RegisterStaticObject]
        //        [Key PageUp] = [Action ScrollPage(1)];    "
        public static KeyBindingDefinition KBD_ScrollPageUp = new KeyBindingDefinition(
            "kbd_editor_ScrollPageUp",
             Key.PageUp,
            layer: KeyBindingLayer.Global);

        [RegisterStaticObject] public static KeyBindingDefinition KBD_Batch_ModeWallLeft = new KeyBindingDefinition(
            nameof(Lang.kbd_batch_ModeWallLeft),
            Key.OemTilde,
            layer: KeyBindingLayer.Batch);
        [RegisterStaticObject] public static KeyBindingDefinition KBD_Batch_ModeLaneLeft = new KeyBindingDefinition(
            nameof(Lang.kbd_batch_ModeLaneLeft),
            Key.D1,
            layer: KeyBindingLayer.Batch);
        [RegisterStaticObject] public static KeyBindingDefinition KBD_Batch_ModeLaneCenter = new KeyBindingDefinition(
            nameof(Lang.kbd_batch_ModeLaneCenter),
            Key.D2,
            layer: KeyBindingLayer.Batch);
        [RegisterStaticObject] public static KeyBindingDefinition KBD_Batch_ModeLaneRight = new KeyBindingDefinition(
            nameof(Lang.kbd_batch_ModeLaneRight),
            Key.D3,
            layer: KeyBindingLayer.Batch);
        [RegisterStaticObject] public static KeyBindingDefinition KBD_Batch_ModeWallRight = new KeyBindingDefinition(
            nameof(Lang.kbd_batch_ModeWallRight),
            Key.D4,
            layer: KeyBindingLayer.Batch);
        [RegisterStaticObject] public static KeyBindingDefinition KBD_Batch_ModeLaneColorful = new KeyBindingDefinition(
            nameof(Lang.kbd_batch_ModeLaneColorful),
            Key.D5,
            layer: KeyBindingLayer.Batch);
        [RegisterStaticObject] public static KeyBindingDefinition KBD_Batch_ModeTap = new KeyBindingDefinition(
            nameof(Lang.kbd_batch_ModeTap),
            Key.T,
            layer: KeyBindingLayer.Batch);
        [RegisterStaticObject] public static KeyBindingDefinition KBD_Batch_ModeHold = new KeyBindingDefinition(
            nameof(Lang.kbd_batch_ModeHold),
            Key.H,
            layer: KeyBindingLayer.Batch);
        [RegisterStaticObject] public static KeyBindingDefinition KBD_Batch_ModeFlick = new KeyBindingDefinition(
            nameof(Lang.kbd_batch_ModeFlick),
            Key.F,
            layer: KeyBindingLayer.Batch);
        [RegisterStaticObject] public static KeyBindingDefinition KBD_Batch_ModeLaneBlock = new KeyBindingDefinition(
            nameof(Lang.kbd_batch_ModeLaneBlock),
            Key.Z,
            layer: KeyBindingLayer.Batch);
        [RegisterStaticObject] public static KeyBindingDefinition KBD_Batch_ModeNormalBell = new KeyBindingDefinition(
            nameof(Lang.kbd_batch_ModeNormalBell),
            Key.E,
            layer: KeyBindingLayer.Batch);
        [RegisterStaticObject] public static KeyBindingDefinition KBD_Batch_ModeClipboard = new KeyBindingDefinition(
            nameof(Lang.kbd_batch_ModeClipboard),
            Key.V,
            layer: KeyBindingLayer.Batch);
        [RegisterStaticObject] public static KeyBindingDefinition KBD_Batch_ModeFilterLanes = new KeyBindingDefinition(
            nameof(Lang.kbd_batch_ModeFilterLanes),
            Key.D6,
            layer: KeyBindingLayer.Batch);
        [RegisterStaticObject] public static KeyBindingDefinition KBD_Batch_ModeFilterDockableObjects = new KeyBindingDefinition(
            nameof(Lang.kbd_batch_ModeFilterDockableObjects),
            Key.Y,
            layer: KeyBindingLayer.Batch);
        [RegisterStaticObject] public static KeyBindingDefinition KBD_Batch_ModeFilterFloatingObjects = new KeyBindingDefinition(
            nameof(Lang.kbd_batch_ModeFilterFloatingObjects),
            Key.U,
            layer: KeyBindingLayer.Batch);

    }
}



