using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Behaviors.BatchMode;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Avalonia.UI.ValueConverters;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenVisualEditor;

public sealed class BatchModeInteractionTests
{
    [AvaloniaFact]
    public void IsBatchMode_AttachesBehaviorAndDisablingCancelsOwnedGesture()
    {
        var editor = CreateEditor();
        var view = new FumenVisualEditorView { DataContext = editor };
        editor.OnViewAfterLoaded(view);

        try
        {
            Assert.DoesNotContain(editor.BatchModeBehavior, Interaction.GetBehaviors(view));

            editor.IsBatchMode = true;

            Assert.Contains(editor.BatchModeBehavior, Interaction.GetBehaviors(view));
            var filterMode = BatchModeBehavior.Submodes.OfType<BatchModeFilterFloatingObjects>().Single();
            var handling = editor.BatchModeBehavior.InteractionCoordinator.HandlePointerPressed(
                editor,
                MouseButton.Left,
                new Point(100, 100),
                KeyModifiers.None,
                filterMode);
            Assert.False(handling.Handled);
            Assert.True(handling.CapturePointer);
            Assert.True(editor.SelectionArea.IsActive);
            Assert.True(editor.BatchModeBehavior.InteractionCoordinator.HasActiveGesture);

            editor.IsBatchMode = false;

            Assert.DoesNotContain(editor.BatchModeBehavior, Interaction.GetBehaviors(view));
            Assert.False(editor.SelectionArea.IsActive);
            Assert.False(editor.BatchModeBehavior.InteractionCoordinator.HasActiveGesture);
        }
        finally
        {
            editor.IsBatchMode = false;
            editor.OnViewBeforeUnload(view);
            DisposeEditor(editor);
        }
    }

    [AvaloniaFact]
    public void PointerPressMatrix_PreservesInputFilterAndAltOverrideSemantics()
    {
        var editor = CreateEditor();
        var coordinator = new BatchModeInteractionCoordinator();
        var tapMode = BatchModeBehavior.Submodes.OfType<BatchModeInputTap>().Single();
        var filterMode = BatchModeBehavior.Submodes.OfType<BatchModeFilterFloatingObjects>().Single();

        try
        {
            var brush = coordinator.HandlePointerPressed(
                editor,
                MouseButton.Left,
                new Point(10, 20),
                KeyModifiers.None,
                tapMode);
            Assert.True(brush.Handled);
            Assert.True(brush.CapturePointer);
            Assert.False(editor.SelectionArea.IsActive);
            var canceledBrush = coordinator.HandlePointerReleased(
                editor,
                MouseButton.Left,
                new Point(12, 24),
                KeyModifiers.Alt);
            Assert.True(canceledBrush.Handled);
            Assert.Empty(editor.Fumen.GetAllDisplayableObjects().OfType<Tap>());
            Assert.Equal(0, editor.UndoRedoManager.UndoActionCount);

            var filter = coordinator.HandlePointerPressed(
                editor,
                MouseButton.Left,
                new Point(10, 20),
                KeyModifiers.None,
                filterMode);
            Assert.False(filter.Handled);
            Assert.True(filter.CapturePointer);
            Assert.True(editor.SelectionArea.IsActive);
            Assert.True(editor.SelectionArea.FilterFunc!(new Bell()));
            Assert.False(editor.SelectionArea.FilterFunc!(new Tap()));

            coordinator.Cancel(editor);
            var altOverride = coordinator.HandlePointerPressed(
                editor,
                MouseButton.Left,
                new Point(10, 20),
                KeyModifiers.Alt,
                tapMode);
            Assert.False(altOverride.Handled);
            Assert.False(altOverride.CapturePointer);
            Assert.False(editor.SelectionArea.IsActive);

            var controlAltFilter = coordinator.HandlePointerPressed(
                editor,
                MouseButton.Left,
                new Point(10, 20),
                KeyModifiers.Control | KeyModifiers.Alt,
                tapMode);
            Assert.False(controlAltFilter.Handled);
            Assert.True(controlAltFilter.CapturePointer);
            Assert.True(editor.SelectionArea.FilterFunc!(new Tap()));
            Assert.False(editor.SelectionArea.FilterFunc!(new Flick()));
        }
        finally
        {
            coordinator.Cancel(editor);
            DisposeEditor(editor);
        }
    }

    [AvaloniaFact]
    public void BrushPlacement_SnapshotsReleasePositionAndModifiersAcrossUndoRedo()
    {
        var editor = CreateEditor();
        var coordinator = new BatchModeInteractionCoordinator();
        var flickMode = BatchModeBehavior.Submodes.OfType<BatchModeInputFlick>().Single();
        var releasePosition = new Point(120, 240);

        try
        {
            var press = coordinator.HandlePointerPressed(
                editor,
                MouseButton.Left,
                new Point(80, 160),
                KeyModifiers.None,
                flickMode);
            var release = coordinator.HandlePointerReleased(
                editor,
                MouseButton.Left,
                releasePosition,
                KeyModifiers.Control);

            Assert.True(press.Handled);
            Assert.True(release.Handled);
            var flick = Assert.Single(editor.Fumen.GetAllDisplayableObjects().OfType<Flick>());
            Assert.True(flick.IsCritical);
            Assert.Equal(1, editor.UndoRedoManager.UndoActionCount);
            var expectedTGrid = (flick.TGrid.Unit, flick.TGrid.Grid);
            var expectedXGrid = (flick.XGrid.Unit, flick.XGrid.Grid);

            editor.UndoRedoManager.Undo(1);
            Assert.Empty(editor.Fumen.GetAllDisplayableObjects().OfType<Flick>());

            editor.UpdateBatchModePointerState(new Point(600, 800), KeyModifiers.None, false);
            editor.UndoRedoManager.Redo(1);

            var redone = Assert.Single(editor.Fumen.GetAllDisplayableObjects().OfType<Flick>());
            Assert.Same(flick, redone);
            Assert.True(redone.IsCritical);
            Assert.Equal(expectedTGrid, (redone.TGrid.Unit, redone.TGrid.Grid));
            Assert.Equal(expectedXGrid, (redone.XGrid.Unit, redone.XGrid.Grid));
        }
        finally
        {
            DisposeEditor(editor);
        }
    }

    [AvaloniaFact]
    public void RightClick_DeletesOnlyTheCurrentSingleInputTypeAsOneUndoableAction()
    {
        var tap = new Tap { TGrid = new TGrid(1), XGrid = new XGrid(0) };
        var bell = new Bell { TGrid = new TGrid(1), XGrid = new XGrid(0) };
        var editor = CreateEditor(tap, bell);
        var coordinator = new BatchModeInteractionCoordinator();
        var tapMode = BatchModeBehavior.Submodes.OfType<BatchModeInputTap>().Single();
        var position = new Point(100, 100);
        editor.RegisterSelectableObject(tap, new Vector2(100, 100), new Vector2(20, 20));
        editor.RegisterSelectableObject(bell, new Vector2(100, 100), new Vector2(20, 20));

        try
        {
            coordinator.HandlePointerPressed(
                editor,
                MouseButton.Right,
                position,
                KeyModifiers.None,
                tapMode);
            var release = coordinator.HandlePointerReleased(
                editor,
                MouseButton.Right,
                position,
                KeyModifiers.None);

            Assert.True(release.Handled);
            Assert.DoesNotContain(editor.Fumen.GetAllDisplayableObjects(), obj => ReferenceEquals(obj, tap));
            Assert.Contains(editor.Fumen.GetAllDisplayableObjects(), obj => ReferenceEquals(obj, bell));
            Assert.Equal(1, editor.UndoRedoManager.UndoActionCount);

            editor.UndoRedoManager.Undo(1);
            Assert.Contains(editor.Fumen.GetAllDisplayableObjects(), obj => ReferenceEquals(obj, tap));
            Assert.Contains(editor.Fumen.GetAllDisplayableObjects(), obj => ReferenceEquals(obj, bell));

            editor.UndoRedoManager.Redo(1);
            Assert.DoesNotContain(editor.Fumen.GetAllDisplayableObjects(), obj => ReferenceEquals(obj, tap));
            Assert.Contains(editor.Fumen.GetAllDisplayableObjects(), obj => ReferenceEquals(obj, bell));
        }
        finally
        {
            DisposeEditor(editor);
        }
    }

    [AvaloniaFact]
    public void RangeDelete_UsesTheFilteredRangeObjectsInsteadOfCurrentSelection()
    {
        var rangeTarget = new Tap { TGrid = new TGrid(1), XGrid = new XGrid(0) };
        var selectedOutsideRange = new Bell
        {
            TGrid = new TGrid(8),
            XGrid = new XGrid(4),
            IsSelected = true
        };
        var editor = CreateEditor(rangeTarget, selectedOutsideRange);

        try
        {
            SelectionAreaKind.Delete.SelectAction(editor, [rangeTarget]);

            Assert.DoesNotContain(editor.Fumen.GetAllDisplayableObjects(), obj => ReferenceEquals(obj, rangeTarget));
            Assert.Contains(editor.Fumen.GetAllDisplayableObjects(), obj => ReferenceEquals(obj, selectedOutsideRange));
            Assert.True(selectedOutsideRange.IsSelected);
            Assert.Equal(1, editor.UndoRedoManager.UndoActionCount);

            editor.UndoRedoManager.Undo(1);
            Assert.Contains(editor.Fumen.GetAllDisplayableObjects(), obj => ReferenceEquals(obj, rangeTarget));
            Assert.Contains(editor.Fumen.GetAllDisplayableObjects(), obj => ReferenceEquals(obj, selectedOutsideRange));
        }
        finally
        {
            DisposeEditor(editor);
        }
    }

    [Fact]
    public void NullToZeroConverter_RestoresOriginalRowHeightContract()
    {
        var converter = new NullToZeroConverter();
        var rowHeight = new object();

        Assert.Equal(
            0d,
            converter.Convert(null!, typeof(double), rowHeight, CultureInfo.InvariantCulture));
        Assert.Same(
            rowHeight,
            converter.Convert(new object(), typeof(double), rowHeight, CultureInfo.InvariantCulture));
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(0d, typeof(object), null!, CultureInfo.InvariantCulture));
    }

    private static FumenVisualEditorViewModel CreateEditor(params OngekiObjectBase[] objects)
    {
        var fumen = new OngekiFumen();
        foreach (var obj in objects)
            fumen.AddObject(obj);

        var project = new EditorProjectDataModel
        {
            AudioDuration = TimeSpan.FromSeconds(30),
            Fumen = fumen
        };
        return new FumenVisualEditorViewModel
        {
            EditorProjectData = project,
            Fumen = fumen,
            IsDirty = false
        };
    }

    private static void DisposeEditor(FumenVisualEditorViewModel editor)
    {
        editor.EditorProjectData?.DisposeRuntimeFiles();
        editor.Setting.Dispose();
    }
}
