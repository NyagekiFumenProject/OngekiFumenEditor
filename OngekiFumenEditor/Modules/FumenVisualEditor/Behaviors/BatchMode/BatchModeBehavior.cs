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
            ["PreviewMouseUp"] = b => new LambdaTriggerAction(o => { b.MouseUp((MouseButtonEventArgs)o); })
        }.ToImmutableDictionary();

    public BatchModeSubmode CurrentSubmode
    {
        get => (BatchModeSubmode)GetValue(CurrentSubmodeProperty);
        set => SetValue(CurrentSubmodeProperty, value);
    }

    private readonly Dictionary<string, TriggerAction> GeneratedClickTriggerActions = new();
    private readonly List<TriggerBase> OldKeyTriggers = new();
    private readonly List<TriggerBase> NewKeyTriggers = new();

    public BatchModeBehavior()
    {
    }

    protected override void OnAttached()
    {
        if (AssociatedObject.DataContext is not FumenVisualEditorViewModel editor)
            return;

        var editorTriggers = Interaction.GetTriggers(AssociatedObject);

        // Create brush key triggers on the FumenVisualEditorView.
        // Temporarily delete existing ones that clash with brush keys.
        var allCommands = CommandDefinitions.SelectMany
            <KeyValuePair<KeyBindingDefinition, BatchModeSubmode>, ((Key, ModifierKeys), BatchModeSubmode)>(kv =>
            [
                ((kv.Key.Key, kv.Key.Modifiers), kv.Value),
                ((kv.Key.Key, kv.Key.Modifiers | ModifierKeys.Shift), kv.Value)
            ]);

        foreach (var ((key, modifiers), submodeType) in allCommands) {
            var existingTriggers = editorTriggers.Where(t =>
                t is ActionMessageKeyBinding am && am.Definition.Key == key && am.Definition.Modifiers == modifiers);
            OldKeyTriggers.AddRange(existingTriggers);
            OldKeyTriggers.ForEach(t => editorTriggers.Remove(t));

            var newTrigger = new KeyTrigger() { Key = key, Modifiers = modifiers };
            newTrigger.Actions.Add(new LambdaTriggerAction(_ => ApplySubmode(submodeType)));
            editorTriggers.Add(newTrigger);
            NewKeyTriggers.Add(newTrigger);
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
    }

    private void ApplySubmode(BatchModeSubmode submode)
    {
        CurrentSubmode = submode;
    }

    #region Mouse handling

    private bool lastLeftClickWasAltClick = false;
    private bool lastRightClickWasAltClick = false;

    private void MouseDown(MouseButtonEventArgs args)
    {
        if (AssociatedObject.DataContext is not FumenVisualEditorViewModel editor)
            return;

        if (args.ChangedButton == MouseButton.Left) {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                // Holding alt forces Normal Mode click behavior.
                lastLeftClickWasAltClick = true;

                if (CurrentSubmode is BatchModeSingleInputSubmode single &&
                    Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                    // Ctrl+Alt will filter for single objects
                    editor.InitializeSelectionArea(SelectionAreaKind.Select);
                    editor.SelectionArea.FilterFunc = o => single.ObjectType.IsInstanceOfType(o);
                }

                return;
            }

            if (CurrentSubmode is BatchModeFilterSubmode) {
                // If it's a FilterSubmode, dragging without Alt will apply the filter.
                editor.InitializeSelectionArea(SelectionAreaKind.Select);
                editor.SelectionArea.FilterFunc = GetFilterFunc();
                return;
            }

            args.Handled = true;
        } else if (args.ChangedButton == MouseButton.Right) {
            editor.InitializeSelectionArea(SelectionAreaKind.Delete);

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                lastRightClickWasAltClick = true;
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                    editor.SelectionArea.FilterFunc = GetFilterFunc();
                }
            }
            else {
                editor.SelectionArea.FilterFunc = GetFilterFunc();
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
                // Let VisualEditorViewModel code handle it
                lastLeftClickWasAltClick = false;
                return;
            }

            if (!lastLeftClickWasAltClick) {
                // Can hold alt while releasing to "cancel" the left click
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                    args.Handled = true;
                    return;
                }

                if (CurrentSubmode is BatchModeInputSubmode inputSubmode) {
                    PerformBrush(inputSubmode);
                    args.Handled = true;
                }
            }
            else {
                lastLeftClickWasAltClick = false;
            }
        } else if (args.ChangedButton == MouseButton.Right) {
            if (!lastRightClickWasAltClick) {
                if (!editor.IsRangeSelecting) {
                    // Delete single
                    if (CurrentSubmode is BatchModeSingleInputSubmode typeSubmode) {
                        PerformRemove(typeSubmode);
                    }
                    args.Handled = true;
                    return;
                }
            }
            else {
                lastRightClickWasAltClick = false;
            }

            if (editor.IsRangeSelecting) {
                editor.ConsumeSelectionArea();
            }

            args.Handled = true;
        }
    }

    private Func<OngekiObjectBase, bool>? GetFilterFunc()
    {
        if (CurrentSubmode is BatchModeFilterSubmode filterSubmode) {
            return filterSubmode.FilterFunction;
        }
        if (CurrentSubmode is BatchModeSingleInputSubmode singleInputSubmode) {
            return obj => singleInputSubmode.ObjectType.IsInstanceOfType(obj);
        }

        return null;
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