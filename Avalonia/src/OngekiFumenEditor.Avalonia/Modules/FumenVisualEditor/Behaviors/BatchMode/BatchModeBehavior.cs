#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Behaviors.BatchMode;

public class BatchModeBehavior : Behavior<FumenVisualEditorView>
{
    public static readonly ImmutableList<BatchModeSubmode> Submodes =
        new List<BatchModeSubmode>
        {
            new BatchModeInputClipboard(),
            new BatchModeInputWallLeft(),
            new BatchModeInputLaneLeft(),
            new BatchModeInputLaneCenter(),
            new BatchModeInputLaneRight(),
            new BatchModeInputWallRight(),
            new BatchModeInputLaneColorful(),
            new BatchModeInputTap(),
            new BatchModeInputHold(),
            new BatchModeInputFlick(),
            new BatchModeInputLaneBlock(),
            new BatchModeInputNormalBell(),
            new BatchModeFilterLanes(),
            new BatchModeFilterDockableObjects(),
            new BatchModeFilterFloatingObjects(),
        }.ToImmutableList();

    public static readonly StyledProperty<BatchModeSubmode> CurrentSubmodeProperty =
        AvaloniaProperty.Register<BatchModeBehavior, BatchModeSubmode>(
            nameof(CurrentSubmode),
            Submodes[0]);

    private readonly BatchModeInteractionCoordinator interactionCoordinator = new();
    private FumenVisualEditorView? associatedView;
    private ContentControl? renderControlHost;
    private IPointer? capturedPointer;

    public BatchModeSubmode CurrentSubmode
    {
        get => GetValue(CurrentSubmodeProperty);
        set => SetValue(CurrentSubmodeProperty, value);
    }

    internal BatchModeInteractionCoordinator InteractionCoordinator => interactionCoordinator;

    protected override void OnAttached()
    {
        base.OnAttached();

        var view = AssociatedObject ?? throw new InvalidOperationException("Batch mode behavior requires an editor view.");
        associatedView = view;
        renderControlHost = view.FindControl<ContentControl>("renderControlHost");
        view.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, true);
        view.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel, true);
        view.AddHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Bubble, true);
        view.AddHandler(InputElement.LostFocusEvent, OnLostFocus, RoutingStrategies.Bubble, true);
        view.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, true);
        view.AddHandler(Control.ContextRequestedEvent, OnContextRequested, RoutingStrategies.Tunnel, true);
    }

    protected override void OnDetaching()
    {
        var view = associatedView;
        if (view is not null)
        {
            view.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
            view.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
            view.RemoveHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost);
            view.RemoveHandler(InputElement.LostFocusEvent, OnLostFocus);
            view.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            view.RemoveHandler(Control.ContextRequestedEvent, OnContextRequested);
        }

        CancelActiveGesture();
        associatedView = null;
        renderControlHost = null;
        base.OnDetaching();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!TryGetEditorAndInputSurface(e.Source, out var editor, out var inputSurface))
            return;

        var button = e.Properties.PointerUpdateKind switch
        {
            PointerUpdateKind.LeftButtonPressed => MouseButton.Left,
            PointerUpdateKind.RightButtonPressed => MouseButton.Right,
            _ => (MouseButton?)null
        };
        if (button is null)
            return;

        var canvasPosition = editor.UpdateBatchModePointerStateFromView(
            e.GetPosition(inputSurface),
            e.KeyModifiers,
            e.Properties.IsLeftButtonPressed);
        var handling = interactionCoordinator.HandlePointerPressed(
            editor,
            button.Value,
            canvasPosition,
            e.KeyModifiers,
            CurrentSubmode);

        if (handling.CapturePointer)
        {
            ReleasePointerCapture();
            capturedPointer = e.Pointer;
            capturedPointer.Capture(inputSurface as IInputElement ?? associatedView);
        }

        if (handling.Handled)
            e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!TryGetEditorAndInputSurface(e.Source, out var editor, out var inputSurface))
            return;

        var button = e.InitialPressMouseButton;
        if (button is not MouseButton.Left and not MouseButton.Right)
            return;

        var canvasPosition = editor.UpdateBatchModePointerStateFromView(
            e.GetPosition(inputSurface),
            e.KeyModifiers,
            e.Properties.IsLeftButtonPressed);
        var handling = interactionCoordinator.HandlePointerReleased(
            editor,
            button,
            canvasPosition,
            e.KeyModifiers);

        ReleasePointerCapture();
        if (handling.Handled)
            e.Handled = true;
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (capturedPointer is null)
            return;

        capturedPointer = null;
        if (associatedView?.DataContext is FumenVisualEditorViewModel editor)
            interactionCoordinator.Cancel(editor);
    }

    private void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (associatedView is { IsKeyboardFocusWithin: false })
            CancelActiveGesture();
    }

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftAlt or Key.RightAlt)
            e.Handled = true;
    }

    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (TryGetInputSurface(e.Source, out _))
            e.Handled = true;
    }

    private bool TryGetEditorAndInputSurface(
        object? source,
        out FumenVisualEditorViewModel editor,
        out Visual inputSurface)
    {
        if (associatedView?.DataContext is not FumenVisualEditorViewModel currentEditor)
        {
            editor = null!;
            inputSurface = null!;
            return false;
        }

        editor = currentEditor;
        return TryGetInputSurface(source, out inputSurface);
    }

    private bool TryGetInputSurface(object? source, out Visual inputSurface)
    {
        var surface = renderControlHost?.Content as Visual;
        if (surface is null || source is not Visual sourceVisual)
        {
            inputSurface = null!;
            return false;
        }

        inputSurface = surface;
        return ReferenceEquals(sourceVisual, surface) ||
               sourceVisual.GetVisualAncestors().Any(x => ReferenceEquals(x, surface));
    }

    private void CancelActiveGesture()
    {
        if (associatedView?.DataContext is FumenVisualEditorViewModel editor)
            interactionCoordinator.Cancel(editor);
        ReleasePointerCapture();
    }

    private void ReleasePointerCapture()
    {
        var pointer = capturedPointer;
        capturedPointer = null;
        pointer?.Capture(null);
    }
}

internal readonly record struct BatchModePointerHandling(bool Handled, bool CapturePointer);

internal sealed class BatchModeInteractionCoordinator
{
    private BatchModeGestureKind activeGesture;
    private BatchModeSubmode? activeSubmode;
    private bool suppressSingleDelete;

    internal bool HasActiveGesture => activeGesture != BatchModeGestureKind.None;

    internal BatchModePointerHandling HandlePointerPressed(
        FumenVisualEditorViewModel editor,
        MouseButton button,
        Point canvasPosition,
        KeyModifiers modifiers,
        BatchModeSubmode submode)
    {
        Cancel(editor);
        editor.UpdateBatchModePointerState(canvasPosition, modifiers, button == MouseButton.Left);
        if (editor.IsLocked || !editor.IsDesignMode)
            return default;

        var alt = modifiers.HasFlag(KeyModifiers.Alt);
        var control = modifiers.HasFlag(KeyModifiers.Control);

        if (button == MouseButton.Left)
        {
            if (alt)
            {
                if (control && submode is BatchModeSingleInputSubmode singleInputSubmode)
                {
                    StartSelection(
                        editor,
                        SelectionAreaKind.Select,
                        canvasPosition,
                        obj => singleInputSubmode.ObjectType.IsInstanceOfType(obj));
                    activeGesture = BatchModeGestureKind.FilterSelection;
                    activeSubmode = submode;
                    return new(false, true);
                }

                return default;
            }

            if (submode is BatchModeFilterSubmode filterSubmode)
            {
                StartSelection(editor, SelectionAreaKind.Select, canvasPosition, filterSubmode.FilterFunction);
                activeGesture = BatchModeGestureKind.FilterSelection;
                activeSubmode = submode;
                return new(false, true);
            }

            if (submode is BatchModeInputSubmode)
            {
                activeGesture = BatchModeGestureKind.Brush;
                activeSubmode = submode;
                return new(true, true);
            }

            return default;
        }

        if (button == MouseButton.Right)
        {
            StartSelection(editor, SelectionAreaKind.Delete, canvasPosition, GetFilterFunction(submode, alt, control));
            activeGesture = BatchModeGestureKind.Delete;
            activeSubmode = submode;
            suppressSingleDelete = alt;
            return new(true, true);
        }

        return default;
    }

    internal BatchModePointerHandling HandlePointerReleased(
        FumenVisualEditorViewModel editor,
        MouseButton button,
        Point canvasPosition,
        KeyModifiers modifiers)
    {
        editor.UpdateBatchModePointerState(canvasPosition, modifiers, false);
        if (activeGesture is BatchModeGestureKind.FilterSelection or BatchModeGestureKind.Delete &&
            editor.SelectionArea.IsActive)
        {
            editor.SelectionArea.EndPoint = canvasPosition;
        }

        if (activeGesture == BatchModeGestureKind.None ||
            (button == MouseButton.Left && activeGesture == BatchModeGestureKind.Delete) ||
            (button == MouseButton.Right && activeGesture != BatchModeGestureKind.Delete))
        {
            return default;
        }

        var gesture = activeGesture;
        var submode = activeSubmode;
        var skipSingleDelete = suppressSingleDelete;
        Reset();

        if (editor.IsLocked || !editor.IsDesignMode)
        {
            if (gesture is BatchModeGestureKind.FilterSelection or BatchModeGestureKind.Delete)
                editor.SelectionArea.IsActive = false;
            return new(gesture is BatchModeGestureKind.Brush or BatchModeGestureKind.Delete, false);
        }

        if (gesture == BatchModeGestureKind.FilterSelection)
            return default;

        if (gesture == BatchModeGestureKind.Brush)
        {
            if (!modifiers.HasFlag(KeyModifiers.Alt) && submode is BatchModeInputSubmode inputSubmode)
                PerformBrush(editor, inputSubmode, canvasPosition, modifiers);
            return new(true, false);
        }

        if (gesture == BatchModeGestureKind.Delete)
        {
            if (editor.SelectionArea.IsActive && !editor.SelectionArea.IsClick())
            {
                editor.ConsumeSelectionArea();
            }
            else
            {
                editor.SelectionArea.IsActive = false;
                if (!skipSingleDelete && submode is BatchModeSingleInputSubmode singleInputSubmode)
                    PerformRemove(editor, singleInputSubmode, canvasPosition);
            }

            return new(true, false);
        }

        return default;
    }

    internal void Cancel(FumenVisualEditorViewModel editor)
    {
        if (activeGesture is BatchModeGestureKind.FilterSelection or BatchModeGestureKind.Delete)
            editor.SelectionArea.IsActive = false;
        Reset();
    }

    private static void StartSelection(
        FumenVisualEditorViewModel editor,
        SelectionAreaKind kind,
        Point canvasPosition,
        Func<OngekiObjectBase, bool>? filterFunction)
    {
        editor.InitializeSelectionArea(kind, canvasPosition);
        editor.SelectionArea.FilterFunc = filterFunction;
    }

    private static Func<OngekiObjectBase, bool>? GetFilterFunction(
        BatchModeSubmode submode,
        bool alt,
        bool control)
    {
        if (alt && !control)
            return null;
        if (submode is BatchModeFilterSubmode filterSubmode)
            return filterSubmode.FilterFunction;
        if (submode is BatchModeSingleInputSubmode singleInputSubmode)
            return obj => singleInputSubmode.ObjectType.IsInstanceOfType(obj);
        return null;
    }

    private static void PerformBrush(
        FumenVisualEditorViewModel editor,
        BatchModeInputSubmode submode,
        Point canvasPosition,
        KeyModifiers modifiers)
    {
        var objects = submode.GenerateObject().ToImmutableArray();
        if (objects.Length == 0)
        {
            if (submode is BatchModeInputClipboard)
                editor.ToastNotify(Lang.CannotBatchInputClipboardEmpty);
            return;
        }

        if (objects.Length > 1)
        {
            if (submode is BatchModeInputClipboard)
                editor.ToastNotify(Lang.CannotBatchInputClipboardNotBrushable);
            Log.LogWarn("Multiple object placement is currently not supported");
            return;
        }

        var ongekiObject = objects[0];
        editor.MoveObjectTo(ongekiObject, canvasPosition);

        var control = modifiers.HasFlag(KeyModifiers.Control);
        var shift = modifiers.HasFlag(KeyModifiers.Shift);
        if (control && submode.ModifyObjectCtrl is { } controlModification)
            controlModification.Function?.Invoke(ongekiObject);
        if (shift && submode.ModifyObjectShift is { } shiftModification)
            shiftModification.Function?.Invoke(ongekiObject);

        if (editor.GetConflictingObject(ongekiObject) is { } conflict)
        {
            editor.NotifyObjectClicked(conflict);
            return;
        }

        var preserveSelection = shift && submode.ModifyObjectShift?.Function is null;
        editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(
            Lang.B.BatchModeAddObject.ToFormatLocalizedString(submode.DisplayName.Text),
            () =>
            {
                editor.Fumen.AddObject(ongekiObject);
                editor.InteractiveManager.GetInteractive(ongekiObject)
                    .OnMoveCanvas(ongekiObject, canvasPosition, editor);

                if (!preserveSelection)
                    editor.ClearSelection();
                if (submode.AutoSelect)
                {
                    var previousPreventMutualExclusion = editor.IsPreventMutualExclusionSelecting;
                    editor.IsPreventMutualExclusionSelecting = preserveSelection;
                    try
                    {
                        editor.NotifyObjectClicked(ongekiObject);
                    }
                    finally
                    {
                        editor.IsPreventMutualExclusionSelecting = previousPreventMutualExclusion;
                    }
                }
            },
            () => editor.RemoveObjects(objects)));
    }

    private static void PerformRemove(
        FumenVisualEditorViewModel editor,
        BatchModeSingleInputSubmode submode,
        Point canvasPosition)
    {
        var hit = editor.QueryHitObjects(canvasPosition)
            .FirstOrDefault(obj => obj.GetType() == submode.ObjectType);
        if (hit is null)
            return;

        editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(
            Lang.B.DeleteSpecificObject.ToFormatLocalizedString(submode.DisplayName.Text),
            () => editor.RemoveObject(hit),
            () => editor.Fumen.AddObject(hit)));
    }

    private void Reset()
    {
        activeGesture = BatchModeGestureKind.None;
        activeSubmode = null;
        suppressSingleDelete = false;
    }

    private enum BatchModeGestureKind
    {
        None,
        Brush,
        FilterSelection,
        Delete
    }
}

public class BatchModeSubmodeNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is BatchModeSubmode submode ? submode.DisplayName.Text : string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class IsInstanceOfToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        Log.LogInfo($"{parameter}");
        Log.LogInfo($"{parameter}");
        return parameter is Type type && (value?.GetType().IsSubclassOf(type) ?? false);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
