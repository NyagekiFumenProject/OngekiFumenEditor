using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Caliburn.Micro;
using Microsoft.Xaml.Behaviors;
using Microsoft.Xaml.Behaviors.Core;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Kernel.KeyBinding;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.UI.KeyBinding.Input;
using OngekiFumenEditor.Utils;
using EventTrigger = Microsoft.Xaml.Behaviors.EventTrigger;
using TriggerAction = Microsoft.Xaml.Behaviors.TriggerAction;
using TriggerBase = Microsoft.Xaml.Behaviors.TriggerBase;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Behaviors.BrushMode;

public class BrushModeBehavior : Behavior<FumenVisualEditorView>
{
    private readonly ImmutableDictionary<KeyBindingDefinition, BrushModeInputObject> CommandDefinitions;
    private readonly ImmutableDictionary<string, TriggerAction> ClickTriggers;

    public BrushModeInputObject CurrentInputObject
    {
        get => (BrushModeInputObject)GetValue(CurrentInputObjectProperty);
        set => SetValue(CurrentInputObjectProperty, value);
    }

    private readonly List<TriggerBase> OldTriggers = new();
    private readonly List<TriggerBase> NewTriggers = new();
    private readonly IFumenEditorClipboard Clipboard;
    
    public BrushModeBehavior()
    {
        CommandDefinitions =  new Dictionary<KeyBindingDefinition, BrushModeInputObject>
        {
            [KeyBindingDefinitions.KBD_Batch_ModeWallLeft] = new BrushModeInputWallLeft(),
            [KeyBindingDefinitions.KBD_Batch_ModeLaneLeft] = new BrushModeInputLaneLeft(),
            [KeyBindingDefinitions.KBD_Batch_ModeLaneCenter] = new BrushModeInputLaneCenter(),
            [KeyBindingDefinitions.KBD_Batch_ModeLaneRight] = new BrushModeInputLaneRight(),
            [KeyBindingDefinitions.KBD_Batch_ModeWallRight] = new BrushModeInputWallRight(),
            [KeyBindingDefinitions.KBD_Batch_ModeLaneColorful] = new BrushModeInputLaneColorful(),
            [KeyBindingDefinitions.KBD_Batch_ModeTap] = new BrushModeInputTap(),
            [KeyBindingDefinitions.KBD_Batch_ModeHold] = new BrushModeInputHold(),
            [KeyBindingDefinitions.KBD_Batch_ModeFlick] = new BrushModeInputFlick(),
            [KeyBindingDefinitions.KBD_Batch_ModeLaneBlock] = new BrushModeInputLaneBlock(),
            [KeyBindingDefinitions.KBD_Batch_ModeNormalBell] = new BrushModeInputNormalBell(),
        }.ToImmutableDictionary();

        ClickTriggers = new Dictionary<string, TriggerAction>
        {
            ["PreviewMouseDown"] = new LambdaTriggerAction(o => { MouseDown((MouseButtonEventArgs)o); }),
            ["PreviewMouseUp"] = new LambdaTriggerAction(o => { MouseUp((MouseButtonEventArgs)o); }),
            ["MouseMove"] = new LambdaTriggerAction(o => { MouseMove((MouseEventArgs)o);})
        }.ToImmutableDictionary();

        Clipboard = IoC.Get<IFumenEditorClipboard>();
    }

    protected override void OnAttached()
    {
        if (AssociatedObject.DataContext is not FumenVisualEditorViewModel editor)
            return;

        var triggerCollection = Interaction.GetTriggers(AssociatedObject);

        // Create brush key triggers on the FumenVisualEditorView.
        // Temporarily delete existing ones that clash with brush keys. 
        foreach (var (key, obj) in CommandDefinitions) {
            var existingTriggers = triggerCollection.Where(t =>
                t is ActionMessageKeyBinding am && am.Definition.Key == key.Key &&
                am.Definition.Modifiers == key.Modifiers);
            OldTriggers.AddRange(existingTriggers);
            OldTriggers.ForEach(t => triggerCollection.Remove(t));

            foreach (var mod in new[] { ModifierKeys.None, ModifierKeys.Shift }) {
                // It's useful to hold down shift as we place multiple lanes, so bind everything to Shift+ as well.
                var newTrigger = new KeyTrigger() { Key = key.Key, Modifiers = mod };
                newTrigger.Actions.Add(new ChangePropertyAction() { TargetObject = this, PropertyName = nameof(CurrentInputObject), Value = obj });
                triggerCollection.Add(newTrigger);
                NewTriggers.Add(newTrigger);
            }
        }

        // Add mouse click events directly on the GlView.
        var glTriggers = Interaction.GetTriggers(AssociatedObject.glView);
        foreach (var (eventName, action) in ClickTriggers) {
            var glTrigger = glTriggers.FirstOrDefault(t => t is EventTrigger et && et.EventName == eventName);
            
            if (glTrigger is null) {
                glTrigger = new EventTrigger(eventName);
                glTriggers.Add(glTrigger);
            }
            
            glTrigger.Actions.Insert(0, action);
        }

        // Pressing alt normally focuses the menu bar.
        // That is annoying when we have Alt+Click bindings, so we disable it.
        AssociatedObject.KeyDown += ConsumeAlt;
    }

    protected override void OnDetaching()
    {
        var triggerCollection = Interaction.GetTriggers(AssociatedObject);
        foreach (var trigger in NewTriggers) {
            triggerCollection.Remove(trigger);
        }

        foreach (var trigger in OldTriggers) {
            triggerCollection.Add(trigger);
        }

        NewTriggers.Clear();
        OldTriggers.Clear();
        
        var glTriggers = Interaction.GetTriggers(AssociatedObject.glView);
        foreach (var (eventName, action) in ClickTriggers) {
            var glTrigger = glTriggers.First(t => t is EventTrigger et && et.EventName == eventName);
            glTrigger.Actions.Remove(action);
        }

        AssociatedObject.KeyDown -= ConsumeAlt;
    }

    #region Mouse handling

    private bool lastLeftClickWasAltClick = false;
    private bool lastRightClickWasAltClick = false;

    private void MouseMove(MouseEventArgs args)
    {
        if (CurrentInputObject is null)
            return;
        if (AssociatedObject.DataContext is not FumenVisualEditorViewModel editor)
            return;

        if ((!lastRightClickWasAltClick && Mouse.RightButton == MouseButtonState.Pressed)
            || (lastLeftClickWasAltClick && Mouse.LeftButton == MouseButtonState.Pressed)) {
            var cursor = editor.CurrentCursorPosition!.Value;

            if (!editor.IsRangeSelecting) {
                if ((cursor.ToSystemNumericsVector2() - editor.SelectionStartPosition).Length() > 15) {
                    // Begin range mode
                    editor.ClearSelection();
                    editor.SelectionVisibility = Visibility.Visible;
                }
                else {
                    // Prevent the VisualEditor mousemove from making a selection rect
                    args.Handled = true;
                }
            }

            if (editor.IsRangeSelecting) {
                editor.SelectionCurrentCursorPosition = new((float)cursor.X,  (float)cursor.Y);
            }
        }
    }

    private void MouseDown(MouseButtonEventArgs args)
    {
        if (AssociatedObject.DataContext is not FumenVisualEditorViewModel editor)
            return;

        var cursor = editor.CurrentCursorPosition!.Value;

        if (args.ChangedButton == MouseButton.Left) {
            // If holding down alt, don't handle, use normal mouse behavior instead
            if ((Keyboard.Modifiers & ModifierKeys.Alt) > 0) {
                lastLeftClickWasAltClick = true;
                editor.SelectionStartPosition = cursor.ToSystemNumericsVector2();
            }

            args.Handled = true;
        } else if (args.ChangedButton == MouseButton.Right) {
            if ((Keyboard.Modifiers & ModifierKeys.Alt) == 0) {
                editor.SelectRegionType = SelectRegionType.Delete;
                editor.SelectionStartPosition = cursor.ToSystemNumericsVector2();
                args.Handled = true;
            }
            else {
                lastRightClickWasAltClick = true;
            }
        }
    }

    private void MouseUp(MouseButtonEventArgs args)
    {
        var editor = IoC.Get<IEditorDocumentManager>()?.CurrentActivatedEditor;
        if (editor is null)
            return;

        if (args.ChangedButton == MouseButton.Left) {
            if (!lastLeftClickWasAltClick || !editor.IsRangeSelecting) {
                PerformBrush();
                args.Handled = true;
            }
        } else if (args.ChangedButton == MouseButton.Right) {
            if (!lastRightClickWasAltClick) {
                if (editor.IsRangeSelecting) {
                    PerformRemoveGroup();
                }
                else {
                    PerformRemove();
                }

                editor.SelectRegionType = SelectRegionType.Select;
                editor.SelectionVisibility = Visibility.Hidden;
                args.Handled = true;
            }
        }

        lastLeftClickWasAltClick = false;
        lastRightClickWasAltClick = false;
    }

    #endregion

    private void PerformBrush()
    {
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) > 0;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) > 0;

        var editor = (FumenVisualEditorViewModel)AssociatedObject.DataContext;

        OngekiTimelineObjectBase ongekiObject = null;

        var objectName = CurrentInputObject?.DisplayName ?? ClipboardObjectName;
        editor.UndoRedoManager.ExecuteAction(new LambdaUndoAction(Resources.BatchModeAddObject.Format(objectName), Redo, Undo));

        return;
        
        void Redo()
        {
            if (CurrentInputObject is null) {
                if (!Clipboard.ContainPastableObjects
                    || !Clipboard.CurrentCopiedObjects.IsOnlyOne(out var clipboardObj)
                    || clipboardObj is not OngekiTimelineObjectBase) {
                    return;
                }
                ongekiObject = (OngekiTimelineObjectBase)clipboardObj.CopyNew();
            }
            else {
                ongekiObject = CurrentInputObject.GenerateObject();
                if (ctrl && CurrentInputObject.ModifyObjectCtrl is { } modCtrl)
                    modCtrl.Function?.Invoke(ongekiObject);
                if (shift && CurrentInputObject.ModifyObjectShift is { } modShift)
                    modShift.Function?.Invoke(ongekiObject);
            }
        
            editor!.MoveObjectTo(ongekiObject, editor.CurrentCursorPosition!.Value);
            editor.Fumen.AddObject(ongekiObject);
            editor.InteractiveManager.GetInteractive(ongekiObject).OnMoveCanvas(ongekiObject, editor.CurrentCursorPosition.Value, editor);
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0)
                editor.ClearSelection();
            if (CurrentInputObject?.IsKeepExistingSelection ?? false)
                editor.NotifyObjectClicked(ongekiObject);
        }

        void Undo()
        {
            if (ongekiObject is null)
                return;
            
            editor!.RemoveObject(ongekiObject);
            ongekiObject = null;
        }
    }

    private void PerformRemove()
    {
        if (CurrentInputObject is null)
            return;

        var editor = (FumenVisualEditorViewModel)AssociatedObject.DataContext;
        var hit = editor.GetHits()
            .Where(kv =>
                kv.Value.Contains(editor.CurrentCursorPosition!.Value) &&
                kv.Key.GetType() == CurrentInputObject.ObjectType)
            .Select(kv => kv.Key).MinBy(o => o.Id);

        if (hit is not null) {
            editor.UndoRedoManager.ExecuteAction(new LambdaUndoAction(Resources.DeleteSpecificObject.Format(CurrentInputObject.DisplayName), Redo, Undo));
        }

        return;

        void Redo()
        {
            editor.RemoveObject(hit);
        }

        void Undo()
        {
            editor.Fumen.AddObject(hit);
        }
    }

    private void PerformRemoveGroup()
    {
        if (CurrentInputObject is null)
            return;

        var editor = (FumenVisualEditorViewModel)AssociatedObject.DataContext;
        var hits = editor.GetRangeObjects().Where(o => o.GetType() == CurrentInputObject.ObjectType).ToArray();

        if (hits.Length == 0) {
            return;
        }

        editor.UndoRedoManager.ExecuteAction(new LambdaUndoAction(Resources.BatchModeDeleteRangeOfObjectType.Format(CurrentInputObject.DisplayName, hits.Length), Redo, Undo));

        return;

        void Redo()
        {
            foreach (var hit in hits) {
                editor.RemoveObject(hit);
            }
        }

        void Undo()
        {
            editor.Fumen.AddObjects(hits);
        }

    }

    private static void ConsumeAlt(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.System && e.SystemKey == Key.LeftAlt || e.SystemKey == Key.RightAlt) {
            e.Handled = true;
        }
    }

    private string ClipboardObjectName => "Clipboard";

    #region Dependency property

    public static readonly DependencyProperty CurrentInputObjectProperty = DependencyProperty.RegisterAttached(nameof(CurrentInputObject), typeof(BrushModeInputObject), typeof(BrushModeBehavior), new PropertyMetadata(null));

    #endregion

}

public class BrushModeInputObjectNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return ((BrushModeInputObject)value)?.DisplayName ?? "Clipboard";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}