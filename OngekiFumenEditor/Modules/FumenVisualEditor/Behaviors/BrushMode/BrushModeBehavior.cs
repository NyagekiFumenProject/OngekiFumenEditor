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
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
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
    private readonly ImmutableDictionary<KeyBindingDefinition, Type> CommandDefinitions;
    private readonly ImmutableDictionary<string, TriggerAction> ClickTriggers;

    public BatchModeSubmode CurrentSubmode
    {
        get => (BatchModeSubmode)GetValue(CurrentSubmodeProperty);
        set => SetValue(CurrentSubmodeProperty, value);
    }

    private readonly List<TriggerBase> OldTriggers = new();
    private readonly List<TriggerBase> NewTriggers = new();
    private readonly IFumenEditorClipboard Clipboard;
    
    public BrushModeBehavior()
    {
        CommandDefinitions =  new Dictionary<KeyBindingDefinition, Type>
        {
            [KeyBindingDefinitions.KBD_Batch_ModeWallLeft] = typeof(BatchModeInputWallLeft),
            [KeyBindingDefinitions.KBD_Batch_ModeLaneLeft] = typeof(BatchModeInputLaneLeft),
            [KeyBindingDefinitions.KBD_Batch_ModeLaneCenter] = typeof(BatchModeInputLaneCenter),
            [KeyBindingDefinitions.KBD_Batch_ModeLaneRight] = typeof(BatchModeInputLaneRight),
            [KeyBindingDefinitions.KBD_Batch_ModeWallRight] = typeof(BatchModeInputWallRight),
            [KeyBindingDefinitions.KBD_Batch_ModeLaneColorful] = typeof(BatchModeInputLaneColorful),
            [KeyBindingDefinitions.KBD_Batch_ModeTap] = typeof(BatchModeInputTap),
            [KeyBindingDefinitions.KBD_Batch_ModeHold] = typeof(BatchModeInputHold),
            [KeyBindingDefinitions.KBD_Batch_ModeFlick] = typeof(BatchModeInputFlick),
            [KeyBindingDefinitions.KBD_Batch_ModeLaneBlock] = typeof(BatchModeInputLaneBlock),
            [KeyBindingDefinitions.KBD_Batch_ModeNormalBell] = typeof(BatchModeInputNormalBell),
            [KeyBindingDefinitions.KBD_Batch_ModeClipboard] = typeof(BatchModeInputClipboard),
            [KeyBindingDefinitions.KBD_Batch_ModeFilterLanes] = typeof(BatchModeFilterLanes),
            [KeyBindingDefinitions.KBD_Batch_ModeFilterDockableObjects] = typeof(BatchModeFilterDockableObjects),
            [KeyBindingDefinitions.KBD_Batch_ModeFilterFloatingObjects] = typeof(BatchModeFilterFloatingObjects),
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
        foreach (var (key, type) in CommandDefinitions) {
            var existingTriggers = triggerCollection.Where(t =>
                t is ActionMessageKeyBinding am && am.Definition.Key == key.Key &&
                am.Definition.Modifiers == key.Modifiers);
            OldTriggers.AddRange(existingTriggers);
            OldTriggers.ForEach(t => triggerCollection.Remove(t));

            foreach (var mod in new[] { ModifierKeys.None, ModifierKeys.Shift }) {
                // It's useful to hold down shift as we place multiple lanes, so bind everything to Shift+ as well.
                var newTrigger = new KeyTrigger() { Key = key.Key, Modifiers = mod };
                newTrigger.Actions.Add(new ChangePropertyAction() { TargetObject = this, PropertyName = nameof(CurrentSubmode), Value = Activator.CreateInstance(type) });
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
        if (Mouse.LeftButton != MouseButtonState.Pressed && Mouse.RightButton != MouseButtonState.Pressed)
            return;
        if (CurrentSubmode is null)
            return;
        if (AssociatedObject.DataContext is not FumenVisualEditorViewModel editor)
            return;

        if (Mouse.RightButton == MouseButtonState.Pressed) {
            editor.SelectionVisibility = Visibility.Visible;
            editor.SelectionCurrentCursorPosition = editor.CurrentCursorPosition?.ToSystemNumericsVector2()
                                                    ?? args.GetPosition(null).ToSystemNumericsVector2();
        }
    }

    private void MouseDown(MouseButtonEventArgs args)
    {
        if (AssociatedObject.DataContext is not FumenVisualEditorViewModel editor)
            return;

        var cursor = editor.CurrentCursorPosition!.Value;

        if (args.ChangedButton == MouseButton.Left) {
            // In all sub-modes, Alt forces normal mouse behavior
            if ((Keyboard.Modifiers & ModifierKeys.Alt) > 0) {
                lastLeftClickWasAltClick = true;
                editor.SelectionStartPosition = cursor.ToSystemNumericsVector2();
                if ((Keyboard.Modifiers & ModifierKeys.Control) > 0) {
                    editor.SelectRegionType = SelectRegionType.SelectFiltered;
                }
                return;
            }

            if (CurrentSubmode is BatchModeFilterSubmode) {
                // If it's a FilterSubmode, dragging without Alt will apply the filter.
                editor.SelectionStartPosition = cursor.ToSystemNumericsVector2();
                return;
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
            if (!lastLeftClickWasAltClick) {
                // Can hold alt while releasing to "cancel" the left click
                if ((Keyboard.Modifiers & ModifierKeys.Alt) == 0) {
                    if (CurrentSubmode is BatchModeInputSubmode inputSubmode)
                        PerformBrush(inputSubmode);
                    else if (CurrentSubmode is BatchModeFilterSubmode filterSubmode && editor.IsRangeSelecting) {
                        editor.SelectionVisibility = Visibility.Hidden;
                        PerformFilterSelect(filterSubmode);
                    }
                }
                args.Handled = true;
            }
        } else if (args.ChangedButton == MouseButton.Right) {
            if (!lastRightClickWasAltClick) {
                if (editor.IsRangeSelecting) {
                    if (CurrentSubmode is BatchModeSingleInputSubmode typeSubmode) {
                        PerformRemoveGroup(typeSubmode);
                    } else if (CurrentSubmode is BatchModeFilterSubmode filterSubmode) {
                        PerformRemoveGroup(filterSubmode);
                    }
                }
                else {
                    if (CurrentSubmode is BatchModeSingleInputSubmode typeSubmode) {
                        PerformRemove(typeSubmode);
                    }
                }

                editor.SelectRegionType = SelectRegionType.Select;
                editor.SelectionVisibility = Visibility.Hidden;
                args.Handled = true;
            }
            else {
                // Alt + RMB clears selection
                // TODO Should this do something else?
                editor.ClearSelection();
            }
        }

        lastLeftClickWasAltClick = false;
        lastRightClickWasAltClick = false;
    }

    #endregion

    private void PerformBrush(BatchModeInputSubmode submode)
    {
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) > 0;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) > 0;

        var editor = (FumenVisualEditorViewModel)AssociatedObject.DataContext;

        ImmutableList<OngekiTimelineObjectBase> ongekiObjects = null;
        ongekiObjects = submode.GenerateObject().ToImmutableList();

        if (ongekiObjects.Count == 0) {
            return;
        }

        if (ongekiObjects.Count > 1) {
            Log.LogWarn("Multiple object placement is currently not supported");
            return;
        }


        var objectName = CurrentSubmode.DisplayName;
        editor.UndoRedoManager.ExecuteAction(new LambdaUndoAction(Resources.BatchModeAddObject.Format(objectName), Redo, Undo));

        return;

        void Redo()
        {
            var ongekiObject = ongekiObjects.Single();

            if (ctrl && submode.ModifyObjectCtrl is { } modCtrl)
                modCtrl.Function?.Invoke(ongekiObject);
            if (shift && submode.ModifyObjectShift is { } modShift)
                modShift.Function?.Invoke(ongekiObject);

            editor!.MoveObjectTo(ongekiObject, editor.CurrentCursorPosition!.Value);
            editor.Fumen.AddObject(ongekiObject);
            editor.InteractiveManager.GetInteractive(ongekiObject).OnMoveCanvas(ongekiObject, editor.CurrentCursorPosition.Value, editor);

            if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0 || submode.ModifyObjectShift?.Function is not null)
                editor.ClearSelection();
            if (submode.AutoSelect)
                editor.NotifyObjectClicked(ongekiObject);
        }

        void Undo()
        {
            if (ongekiObjects is null)
                return;

            editor.RemoveObjects(ongekiObjects);
            ongekiObjects = null;
        }
    }

    private void PerformFilterSelect(BatchModeFilterSubmode submode)
    {
        var editor = (FumenVisualEditorViewModel)AssociatedObject.DataContext;
        var hits = editor.GetRangeObjects().Where(o => submode.FilterFunction(o)).ToList();

        foreach (var hit in hits) {
            if (hit is OngekiMovableObjectBase selectable)
                selectable.IsSelected = true;
        }
        IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(editor);
    }

    private void PerformRemove(BatchModeSingleInputSubmode submode)
    {
        var editor = (FumenVisualEditorViewModel)AssociatedObject.DataContext;
        var hit = editor.GetHits()
            .Where(kv =>
                kv.Value.Contains(editor.CurrentCursorPosition!.Value) &&
                kv.Key.GetType() == submode.ObjectType)
            .Select(kv => kv.Key).MinBy(o => o.Id);

        if (hit is not null) {
            editor.UndoRedoManager.ExecuteAction(new LambdaUndoAction(Resources.DeleteSpecificObject.Format(submode.DisplayName), Redo, Undo));
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

    private void PerformRemoveGroup(BatchModeSingleInputSubmode submode)
    {
        PerformRemoveGroup(obj => obj.GetType() == submode.ObjectType, submode.DisplayName);
    }

    private void PerformRemoveGroup(BatchModeFilterSubmode filter)
    {
        PerformRemoveGroup(filter.FilterFunction, filter.DisplayName);
    }

    private void PerformRemoveGroup(Func<OngekiObjectBase, bool> filterFunc, string displayName)
    {
        var editor = (FumenVisualEditorViewModel)AssociatedObject.DataContext;
        var hits = editor.GetRangeObjects().Where(filterFunc).ToArray();

        if (hits.Length == 0) {
            return;
        }

        editor.UndoRedoManager.ExecuteAction(new LambdaUndoAction(Resources.BatchModeDeleteRangeOfObjectType.Format(displayName, hits.Length), Redo, Undo));

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

    public static readonly DependencyProperty CurrentSubmodeProperty = DependencyProperty.RegisterAttached(nameof(CurrentSubmode), typeof(BatchModeSubmode), typeof(BrushModeBehavior), new PropertyMetadata(null));

    #endregion

}

public class BrushModeObjectNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return ((BatchModeSubmode)value)?.DisplayName ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class IsInstanceOfToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        Log.LogInfo($"{parameter}");
        Log.LogInfo($"{parameter}");
        return value?.GetType().IsSubclassOf((Type)parameter!) ?? false
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}