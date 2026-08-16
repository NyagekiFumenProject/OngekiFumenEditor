using Avalonia.Headless.XUnit;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Modules.Dialogs.ViewModels;
using Gekimini.Avalonia.Modules.Window.ViewModels;
using Gekimini.Avalonia.Modules.Window.Views;
using Gekimini.Avalonia.Platforms.Services.Window;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels.Dialog;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenVisualEditor;

public sealed class ConnectableObjectBrushTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task CancelOrClose_DoesNotAddObjectsOrHistory(bool? dialogResult)
    {
        var (editor, fumen, lane) = CreateEditor();
        try
        {
            var windowManager = new RecordingWindowManager { Result = dialogResult };
            var dialogManager = new StubDialogManager();
            var operation = new LaneOperationViewModel(lane);

            await operation.BrushAlongLaneCoreAsync(
                editor,
                [new Tap()],
                windowManager,
                dialogManager);

            Assert.Equal(1, windowManager.ShowDialogCallCount);
            Assert.Empty(fumen.Taps);
            Assert.Equal(0, editor.UndoRedoManager.UndoActionCount);
            Assert.Empty(dialogManager.Messages);
        }
        finally
        {
            editor.Setting.Dispose();
        }
    }

    [AvaloniaFact]
    public async Task ReversedRange_DoesNotAddObjectsOrHistory()
    {
        var (editor, fumen, lane) = CreateEditor();
        try
        {
            var windowManager = new RecordingWindowManager
            {
                Result = true,
                ConfigureDialog = dialog =>
                {
                    dialog.BeginTGrid = new TGrid(4);
                    dialog.EndTGrid = new TGrid(1);
                }
            };
            var operation = new LaneOperationViewModel(lane);

            await operation.BrushAlongLaneCoreAsync(
                editor,
                [new Tap()],
                windowManager,
                new StubDialogManager());

            Assert.Empty(fumen.Taps);
            Assert.Equal(0, editor.UndoRedoManager.UndoActionCount);
        }
        finally
        {
            editor.Setting.Dispose();
        }
    }

    [AvaloniaFact]
    public async Task ConfirmedRange_AddsObjectsAsOneUndoableAction()
    {
        var (editor, fumen, lane) = CreateEditor();
        try
        {
            var operation = new LaneOperationViewModel(lane);

            await operation.BrushAlongLaneCoreAsync(
                editor,
                [new Tap()],
                new RecordingWindowManager { Result = true },
                new StubDialogManager());

            var generatedObjects = fumen.Taps.ToArray();
            Assert.NotEmpty(generatedObjects);
            Assert.Equal(1, editor.UndoRedoManager.UndoActionCount);
            Assert.All(generatedObjects, tap =>
            {
                Assert.InRange(tap.TGrid, lane.MinTGrid, lane.MaxTGrid);
                Assert.NotNull(tap.XGrid);
            });

            editor.UndoRedoManager.Undo(1);
            Assert.Empty(fumen.Taps);

            editor.UndoRedoManager.Redo(1);
            Assert.Equal(generatedObjects, fumen.Taps.ToArray());
        }
        finally
        {
            editor.Setting.Dispose();
        }
    }

    private static (FumenVisualEditorViewModel Editor, OngekiFumen Fumen, LaneLeftStart Lane) CreateEditor()
    {
        var lane = new LaneLeftStart
        {
            RecordId = 1,
            TGrid = new TGrid(0),
            XGrid = new XGrid(0)
        };
        lane.AddChildObject(new LaneLeftNext
        {
            TGrid = new TGrid(4),
            XGrid = new XGrid(4)
        });

        var fumen = new OngekiFumen();
        fumen.AddObject(lane);
        return (new FumenVisualEditorViewModel
        {
            EditorContext = new EditorContext { Fumen = fumen }
        }, fumen, lane);
    }

    private sealed class RecordingWindowManager : IWindowManager
    {
        public bool? Result { get; init; }

        public Action<BrushTGridRangeDialogViewModel>? ConfigureDialog { get; init; }

        public int ShowDialogCallCount { get; private set; }

        public WindowViewBase FindExistingWindow(WindowViewModelBase windowViewModel) => null!;

        public Task ShowWindowAsync(WindowViewBase windowView) => Task.CompletedTask;

        public Task<bool?> ShowDialogAsync(WindowViewBase windowView) => Task.FromResult(Result);

        public Task TryCloseWindowAsync(WindowViewBase windowView, bool dialogResult) => Task.CompletedTask;

        public Task ShowWindowAsync(WindowViewModelBase windowViewModel) => Task.CompletedTask;

        public Task<bool?> ShowDialogAsync(WindowViewModelBase windowViewModel)
        {
            ShowDialogCallCount++;
            ConfigureDialog?.Invoke(Assert.IsType<BrushTGridRangeDialogViewModel>(windowViewModel));
            return Task.FromResult(Result);
        }

        public Task TryCloseWindowAsync(WindowViewModelBase windowViewModelBase, bool dialogResult) =>
            Task.CompletedTask;
    }

    private sealed class StubDialogManager : IDialogManager
    {
        public List<string> Messages { get; } = [];

        public Task<T> ShowDialog<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] T>()
            where T : DialogViewModelBase => throw new NotSupportedException();

        public Task ShowDialog(DialogViewModelBase dialogViewModel) => throw new NotSupportedException();

        public Task ShowMessageDialog(string content, DialogMessageType messageType = DialogMessageType.Info)
        {
            Messages.Add(content);
            return Task.CompletedTask;
        }

        public Task<bool> ShowComfirmDialog(
            string content,
            string? title = null,
            string? yesButtonContent = null,
            string? noButtonContent = null) => Task.FromResult(true);
    }
}
