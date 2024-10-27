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

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Behaviors.BatchMode;

public class BatchModeBehavior : Behavior<FumenVisualEditorView>
{
    private static readonly ImmutableDictionary<KeyBindingDefinition, BatchModeSubmode> CommandDefinitions =
        new Dictionary<KeyBindingDefinition, Type>
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
        }.ToImmutableDictionary(kv => kv.Key, kv => (BatchModeSubmode)Activator.CreateInstance(kv.Value));

    private static readonly ImmutableDictionary<string, Func<BatchModeBehavior, TriggerAction>> ClickTriggers =
        new Dictionary<string, Func<BatchModeBehavior, TriggerAction>>()
        {
            ["PreviewMouseDown"] = b => new LambdaTriggerAction(o => { b.MouseDown((MouseButtonEventArgs)o); }),
            ["PreviewMouseUp"] = b => new LambdaTriggerAction(o => { b.MouseUp((MouseButtonEventArgs)o); }),
            ["MouseMove"] = b => new LambdaTriggerAction(o => { b.MouseMove((MouseEventArgs)o);})
        }.ToImmutableDictionary();

    public BatchModeSubmode CurrentSubmode
    {
        get => (BatchModeSubmode)GetValue(CurrentSubmodeProperty);
        set => SetValue(CurrentSubmodeProperty, value);
    }

    private readonly Dictionary<string, TriggerAction> GeneratedClickTriggerActions = new();
    private readonly List<TriggerBase> OldKeyTriggers = new();
    private readonly List<TriggerBase> NewKeyTriggers = new();

    private readonly IFumenEditorClipboard Clipboard;
    
    public BatchModeBehavior()
    {
        Clipboard = IoC.Get<IFumenEditorClipboard>();
    }

    protected override void OnAttached()
    {
        if (AssociatedObject.DataContext is not FumenVisualEditorViewModel editor)
            return;

        var triggerCollection = Interaction.GetTriggers(AssociatedObject);

        // Create brush key triggers on the FumenVisualEditorView.
        // Temporarily delete existing ones that clash with brush keys. 
        foreach (var (key, submodeType) in CommandDefinitions) {
            var existingTriggers = triggerCollection.Where(t =>
                t is ActionMessageKeyBinding am && am.Definition.Key == key.Key &&
                am.Definition.Modifiers == key.Modifiers);
            OldKeyTriggers.AddRange(existingTriggers);
            OldKeyTriggers.ForEach(t => triggerCollection.Remove(t));

            foreach (var mod in new[] { ModifierKeys.None, ModifierKeys.Shift }) {
                // It's useful to hold down shift as we place multiple lanes, so bind everything to Shift+ as well.
                var newTrigger = new KeyTrigger() { Key = key.Key, Modifiers = mod };
                newTrigger.Actions.Add(new LambdaTriggerAction(_ => ApplySubmode(submodeType)));
                triggerCollection.Add(newTrigger);
                NewKeyTriggers.Add(newTrigger);
            }
        }

        // Add mouse click events directly on the GlView.
        var glTriggers = Interaction.GetTriggers(AssociatedObject.glView);
        foreach (var (eventName, actionGenerator) in ClickTriggers) {
            var glTrigger = glTriggers.FirstOrDefault(t => t is EventTrigger et && et.EventName == eventName);

            if (glTrigger is null) {
                glTrigger = new EventTrigger(eventName);
                glTriggers.Add(glTrigger);
            }

            var action = actionGenerator.Invoke(this);
            glTrigger.Actions.Insert(0, action);
            GeneratedClickTriggerActions[eventName] = action;
        }

        // Pressing alt normally focuses the menu bar.
        // That is annoying when we have Alt+Click bindings, so disable it.
        AssociatedObject.KeyDown += ConsumeAlt;
    }

    protected override void OnDetaching()
    {
        var triggerCollection = Interaction.GetTriggers(AssociatedObject);
        foreach (var trigger in NewKeyTriggers) {
            triggerCollection.Remove(trigger);
        }

        foreach (var trigger in OldKeyTriggers) {
            triggerCollection.Add(trigger);
        }

        NewKeyTriggers.Clear();
        OldKeyTriggers.Clear();
        
        var glTriggers = Interaction.GetTriggers(AssociatedObject.glView);
        foreach (var (eventName, action) in GeneratedClickTriggerActions) {
            var glTrigger = glTriggers.First(t => t is EventTrigger et && et.EventName == eventName);
            glTrigger.Actions.Remove(action);
        }

        GeneratedClickTriggerActions.Clear();

        AssociatedObject.KeyDown -= ConsumeAlt;

        if (AssociatedObject.DataContext is FumenVisualEditorViewModel edtior) {
            edtior.SelectRegionType = SelectRegionType.Select;
        }
    }

    private void ApplySubmode(BatchModeSubmode submode)
    {
        CurrentSubmode = submode;
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
            // In normal mode, right mouse is not used to drag, so we manually do that here
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
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                editor.SelectRegionType = SelectRegionType.Select;
                lastLeftClickWasAltClick = true;
                editor.SelectionStartPosition = cursor.ToSystemNumericsVector2();
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                    editor.SelectRegionType = SelectRegionType.SelectFiltered;
                    editor.SelectionVisibility = Visibility.Visible;
                }
                return;
            }

            if (CurrentSubmode is BatchModeFilterSubmode) {
                // If it's a FilterSubmode, dragging without Alt will apply the filter.
                editor.SelectionStartPosition = cursor.ToSystemNumericsVector2();
                editor.SelectRegionType = SelectRegionType.SelectFiltered;
                return;
            }

            args.Handled = true;
        } else if (args.ChangedButton == MouseButton.Right) {
            editor.SelectionStartPosition = cursor.ToSystemNumericsVector2();
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                lastRightClickWasAltClick = true;
                editor.SelectRegionType = SelectRegionType.Delete;
            }
            else {
                editor.SelectRegionType = SelectRegionType.DeleteFiltered;
            }

            args.Handled = true;
        }
    }

    private void MouseUp(MouseButtonEventArgs args)
    {
        var editor = IoC.Get<IEditorDocumentManager>()?.CurrentActivatedEditor;
        if (editor is null)
            return;

        if (args.ChangedButton == MouseButton.Left) {
            if (editor.IsRangeSelecting) {
                // Selection mode
                Func<OngekiObjectBase, bool> filterFunc = null;
                if (editor.SelectRegionType == SelectRegionType.SelectFiltered && CurrentSubmode is BatchModeFilterSubmode filterSubmode) {
                    filterFunc = filterSubmode.FilterFunction;
                } else if (editor.SelectRegionType == SelectRegionType.SelectFiltered && CurrentSubmode is BatchModeSingleInputSubmode singleInputSubmode) {
                    filterFunc = o => o.GetType() == singleInputSubmode.ObjectType || o.GetType().IsSubclassOf(singleInputSubmode.ObjectType);
                }

                if (filterFunc != null) {
                    editor.SelectionVisibility = Visibility.Hidden;
                    PerformFilterSelect(filterFunc);
                    args.Handled = true;
                }

                editor.SelectRegionType = SelectRegionType.Select;
                // otherwise fall through to FumenVisualEditorViewModel click handler
            }

            if (!lastLeftClickWasAltClick) {
                // Can hold alt while releasing to "cancel" the left click
                if ((Keyboard.Modifiers & ModifierKeys.Alt) == 0) {
                    if (CurrentSubmode is BatchModeInputSubmode inputSubmode) {
                        if (inputSubmode is BatchModeSingleInputSubmode singleInputSubmode
                            && editor.GetHits().Where(kv =>
                                    kv.Value.Contains(editor.CurrentCursorPosition!.Value) &&
                                    singleInputSubmode.ObjectType.IsInstanceOfType(kv.Key)).Select(kv => kv.Key)
                                .FirstOrDefault() is { } hit) {
                            editor.NotifyObjectClicked(hit);
                        }
                        else {
                            PerformBrush(inputSubmode);
                        }
                    }
                    else if (CurrentSubmode is BatchModeFilterSubmode filterSubmode && editor.IsRangeSelecting) {
                        editor.SelectionVisibility = Visibility.Hidden;
                        PerformFilterSelect(filterSubmode.FilterFunction);
                    }
                }
                args.Handled = true;
            }
            else {
                lastLeftClickWasAltClick = false;
            }
        } else if (args.ChangedButton == MouseButton.Right) {
            if (!lastRightClickWasAltClick) {
                if (editor.IsRangeSelecting) {
                    // Drag filter delete
                    if (CurrentSubmode is BatchModeSingleInputSubmode typeSubmode) {
                        PerformRemoveGroup(typeSubmode);
                    } else if (CurrentSubmode is BatchModeFilterSubmode filterSubmode) {
                        PerformRemoveGroup(filterSubmode);
                    }
                }
                else {
                    // Delete single
                    if (CurrentSubmode is BatchModeSingleInputSubmode typeSubmode) {
                        PerformRemove(typeSubmode);
                    }
                }

                editor.SelectionVisibility = Visibility.Hidden;
            }
            else {
                lastRightClickWasAltClick = false;
                if (editor.IsRangeSelecting) {
                    PerformRemoveGroup(null, Resources.Objects);
                }
            }

            if (editor.IsRangeSelecting) {
                // End selection
                editor.SelectRegionType = SelectRegionType.Select;
                editor.SelectionVisibility = Visibility.Hidden;
            }

            args.Handled = true;
        }
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
            if (submode is BatchModeInputClipboard)
                editor.ToastNotify(Resources.CannotBatchInputClipboardEmpty);
            return;
        }

        if (ongekiObjects.Count > 1) {
            if (submode is BatchModeInputClipboard)
                editor.ToastNotify(Resources.CannotBatchInputClipboardNotBrushable);
            Log.LogWarn("Multiple object placement is currently not supported");
            return;
        }

        // TODO Support multiple objects in clipboard
        var ongekiObject = ongekiObjects.Single();
        editor!.MoveObjectTo(ongekiObject, editor.CurrentCursorPosition!.Value);
        if (ctrl && submode.ModifyObjectCtrl is { } modCtrl)
            modCtrl.Function?.Invoke(ongekiObject);
        if (shift && submode.ModifyObjectShift is { } modShift)
            modShift.Function?.Invoke(ongekiObject);

        // Find existing objects of the same type at the same position.
        if (editor.GetConflictingObject(ongekiObject) is { } clash) {
            editor.NotifyObjectClicked(clash);
            return;
        }

        var objectName = CurrentSubmode.DisplayName;
        editor.UndoRedoManager.ExecuteAction(new LambdaUndoAction(Resources.BatchModeAddObject.Format(objectName), Redo, Undo));

        return;

        void Redo()
        {
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
        }
    }

    // TODO Refactor into `SelectionBox` class or something
    private void PerformFilterSelect(Func<OngekiObjectBase, bool> filterFunction)
    {
        var editor = (FumenVisualEditorViewModel)AssociatedObject.DataContext;
        var hits = editor.GetRangeObjects().Where(filterFunction).ToList();

        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) {
            editor.ClearSelection();
        }

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

    private void PerformRemoveGroup(Func<OngekiObjectBase, bool>? filterFunc, string displayName)
    {
        var editor = (FumenVisualEditorViewModel)AssociatedObject.DataContext;
        var hits = editor.GetRangeObjects().Where(filterFunc ?? (_ => true)).ToArray();

        if (hits.Length == 0) {
            return;
        }

        editor.UndoRedoManager.ExecuteAction(new LambdaUndoAction(
            Resources.BatchModeDeleteRangeOfObjectType.Format(displayName, hits.Length), Redo, Undo));

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

    #region Dependency property

    public static readonly DependencyProperty CurrentSubmodeProperty = DependencyProperty.RegisterAttached(nameof(CurrentSubmode), typeof(BatchModeSubmode), typeof(BatchModeBehavior), new(CommandDefinitions[KeyBindingDefinitions.KBD_Batch_ModeClipboard]));

    #endregion

}

public class BatchModeSubmodeNameConverter : IValueConverter
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