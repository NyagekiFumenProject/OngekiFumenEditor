using System.Diagnostics;
using Avalonia.Headless.XUnit;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Modules.Dialogs.ViewModels;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater.DefaultImpl.Factory;
using OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater.OgkrImpl.Factory;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.InterpolateAll;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenVisualEditor;

public sealed class InterpolateAllCommandTests
{
    [Fact]
    public void ResolveCurveInterpolaterFactory_SelectsRequestedMode()
    {
        Assert.Same(
            DefaultCurveInterpolaterFactory.Default,
            InterpolateAllCommandHandlerBase<InterpolateAllCommandDefinition>
                .ResolveCurveInterpolaterFactory(false));
        Assert.Same(
            XGridLimitedCurveInterpolaterFactory.Default,
            InterpolateAllCommandHandlerBase<InterpolateAllCommandDefinition>
                .ResolveCurveInterpolaterFactory(true));
    }

    [AvaloniaFact]
    public async Task CancelledConfirmation_DoesNotModifyFumenOrHistory()
    {
        var (editor, fumen, originalLane) = CreateCurvedEditor();
        var editorManager = new StubEditorDocumentManager { Current = editor };
        var dialogManager = new StubDialogManager { ConfirmResult = false };
        var handler = new InterpolateAllCommandHandler(editorManager, dialogManager);

        await handler.Run(new Command(new InterpolateAllCommandDefinition()));

        Assert.Equal(1, dialogManager.ConfirmCallCount);
        Assert.Same(originalLane, Assert.Single(fumen.Lanes));
        Assert.Equal(0, editor.UndoRedoManager.UndoActionCount);
        Assert.False(editor.IsLocked);
    }

    [AvaloniaFact]
    public async Task NoCurvedLane_DoesNotCreateUndoAction()
    {
        var fumen = new OngekiFumen();
        var lane = new LaneLeftStart
        {
            RecordId = 1,
            TGrid = new TGrid(0),
            XGrid = new XGrid(0)
        };
        fumen.AddObject(lane);
        var editor = new FumenVisualEditorViewModel { Fumen = fumen };
        var dialogManager = new StubDialogManager { ConfirmResult = true };
        var handler = new InterpolateAllCommandHandler(
            new StubEditorDocumentManager { Current = editor },
            dialogManager);

        await handler.Run(new Command(new InterpolateAllCommandDefinition()));

        Assert.Equal(1, dialogManager.ConfirmCallCount);
        Assert.Same(lane, Assert.Single(fumen.Lanes));
        Assert.Equal(0, editor.UndoRedoManager.UndoActionCount);
        Assert.False(editor.IsLocked);
    }

    [AvaloniaFact]
    public async Task InterpolateAllCommand_ReplacesLaneAsOneUndoableAction()
    {
        var (editor, fumen, originalLane) = CreateCurvedEditor();
        var editorManager = new StubEditorDocumentManager { Current = editor };
        var dialogManager = new StubDialogManager { ConfirmResult = true };
        var handler = new InterpolateAllCommandHandler(editorManager, dialogManager);

        await handler.Run(new Command(new InterpolateAllCommandDefinition()));

        var generatedLanes = fumen.Lanes.Where(x => !ReferenceEquals(x, originalLane)).ToArray();
        Assert.NotEmpty(generatedLanes);
        Assert.DoesNotContain(fumen.Lanes, x => ReferenceEquals(x, originalLane));
        Assert.Equal(1, editor.UndoRedoManager.UndoActionCount);
        Assert.False(editor.IsLocked);

        editor.UndoRedoManager.Undo(1);
        Assert.Same(originalLane, Assert.Single(fumen.Lanes));

        editor.UndoRedoManager.Redo(1);
        Assert.DoesNotContain(fumen.Lanes, x => ReferenceEquals(x, originalLane));
        Assert.NotEmpty(fumen.Lanes);
    }

    [AvaloniaFact]
    public async Task XGridLimitedInterpolateCommand_GeneratesIntegralXGridPoints()
    {
        var (editor, fumen, originalLane) = CreateCurvedEditor();
        var editorManager = new StubEditorDocumentManager { Current = editor };
        var dialogManager = new StubDialogManager { ConfirmResult = true };
        var handler = new InterpolateAllWithXGridLimitCommandHandler(editorManager, dialogManager);

        await handler.Run(new Command(new InterpolateAllWithXGridLimitCommandDefinition()));

        var generatedLanes = fumen.Lanes.Where(x => !ReferenceEquals(x, originalLane)).ToArray();
        Assert.NotEmpty(generatedLanes);
        Assert.All(
            generatedLanes.SelectMany(x => x.GetDisplayableObjects()).OfType<OngekiMovableObjectBase>(),
            obj => Assert.Equal(0, obj.XGrid.Grid));
        Assert.False(editor.IsLocked);
    }

    [AvaloniaFact]
    public async Task InterpolateAllCommand_RetargetsDockableObjectsAcrossUndoAndRedo()
    {
        var (editor, fumen, originalLane) = CreateCurvedEditor();
        var tap = new Tap
        {
            TGrid = new TGrid(1),
            XGrid = new XGrid(1),
            ReferenceLaneStart = originalLane
        };
        fumen.AddObject(tap);
        var handler = new InterpolateAllCommandHandler(
            new StubEditorDocumentManager { Current = editor },
            new StubDialogManager { ConfirmResult = true });

        await handler.Run(new Command(new InterpolateAllCommandDefinition()));

        var generatedLane = Assert.Single(fumen.Lanes);
        Assert.Same(generatedLane, tap.ReferenceLaneStart);

        editor.UndoRedoManager.Undo(1);
        Assert.Same(originalLane, tap.ReferenceLaneStart);

        editor.UndoRedoManager.Redo(1);
        Assert.Same(generatedLane, tap.ReferenceLaneStart);
    }

    [AvaloniaFact]
    public async Task ProcessingFailure_UnlocksEditor()
    {
        var (editor, _, _) = CreateCurvedEditor();
        var handler = new ThrowingInterpolateAllCommandHandler(
            new StubEditorDocumentManager { Current = editor },
            new StubDialogManager { ConfirmResult = true });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Run(new Command(new InterpolateAllCommandDefinition())));

        Assert.False(editor.IsLocked);
        Assert.Equal(0, editor.UndoRedoManager.UndoActionCount);
    }

    [AvaloniaFact]
    public async Task LargeChart_InterpolatesWithinBudgetAsSingleAction()
    {
        const int laneCount = 128;
        var fumen = new OngekiFumen();
        for (var i = 0; i < laneCount; i++)
            fumen.AddObject(CreateCurvedLane(i, i * 3));

        var editor = new FumenVisualEditorViewModel { Fumen = fumen };
        var handler = new InterpolateAllCommandHandler(
            new StubEditorDocumentManager { Current = editor },
            new StubDialogManager { ConfirmResult = true });
        var stopwatch = Stopwatch.StartNew();

        await handler.Run(new Command(new InterpolateAllCommandDefinition()));

        stopwatch.Stop();
        Assert.Equal(laneCount, fumen.Lanes.Count);
        Assert.Equal(1, editor.UndoRedoManager.UndoActionCount);
        Assert.False(editor.IsLocked);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Interpolating {laneCount} lanes took {stopwatch.Elapsed}.");
    }

    [AvaloniaFact]
    public async Task Update_OnlyEnablesWithAnActiveFumen()
    {
        var editorManager = new StubEditorDocumentManager();
        var handler = new InterpolateAllCommandHandler(editorManager, new StubDialogManager());
        var command = new Command(new InterpolateAllCommandDefinition());

        await handler.Update(command);
        Assert.False(command.Enabled);

        var (editor, _, _) = CreateCurvedEditor();
        editorManager.Current = editor;
        await handler.Update(command);
        Assert.True(command.Enabled);
    }

    private static (FumenVisualEditorViewModel Editor, OngekiFumen Fumen, LaneLeftStart OriginalLane) CreateCurvedEditor()
    {
        var fumen = new OngekiFumen();
        var lane = CreateCurvedLane(1, 0);
        fumen.AddObject(lane);

        return (new FumenVisualEditorViewModel { Fumen = fumen }, fumen, lane);
    }

    private static LaneLeftStart CreateCurvedLane(int recordId, int tGridOffset)
    {
        var lane = new LaneLeftStart
        {
            RecordId = recordId,
            TGrid = new TGrid(tGridOffset),
            XGrid = new XGrid(0)
        };
        var child = new LaneLeftNext
        {
            TGrid = new TGrid(tGridOffset + 2),
            XGrid = new XGrid(2),
            CurvePrecision = 0.25f
        };
        child.AddControlObject(new LaneCurvePathControlObject
        {
            TGrid = new TGrid(tGridOffset + 1),
            XGrid = new XGrid(8)
        });
        lane.AddChildObject(child);
        return lane;
    }

    private sealed class StubEditorDocumentManager : IEditorDocumentManager
    {
        private FumenVisualEditorViewModel? current;

        public FumenVisualEditorViewModel Current
        {
            get => current!;
            set => current = value;
        }

        public FumenVisualEditorViewModel CurrentActivatedEditor => current!;

        public event IEditorDocumentManager.NotifyCreateFunc OnNotifyCreated
        {
            add { }
            remove { }
        }

        public event IEditorDocumentManager.ActivateEditorChangedFunc OnActivateEditorChanged
        {
            add { }
            remove { }
        }

        public event IEditorDocumentManager.NotifyDestoryFunc OnNotifyDestoryed
        {
            add { }
            remove { }
        }

        public IEnumerable<FumenVisualEditorViewModel> GetCurrentEditors() =>
            current is null ? [] : [current];

        public void NotifyActivate(FumenVisualEditorViewModel editor) => current = editor;

        public void NotifyDeactivate(FumenVisualEditorViewModel editor)
        {
            if (ReferenceEquals(current, editor))
                current = null;
        }

        public void NotifyCreate(FumenVisualEditorViewModel editor) => current = editor;

        public void NotifyDestory(FumenVisualEditorViewModel editor)
        {
            if (ReferenceEquals(current, editor))
                current = null;
        }
    }

    private sealed class StubDialogManager : IDialogManager
    {
        public bool ConfirmResult { get; init; } = true;

        public int ConfirmCallCount { get; private set; }

        public Task<T> ShowDialog<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] T>()
            where T : DialogViewModelBase => throw new NotSupportedException();

        public Task ShowDialog(DialogViewModelBase dialogViewModel) => throw new NotSupportedException();

        public Task ShowMessageDialog(string content, DialogMessageType messageType = DialogMessageType.Info) =>
            throw new NotSupportedException();

        public Task<bool> ShowComfirmDialog(string content, string? title = null, string? yesButtonContent = null,
            string? noButtonContent = null)
        {
            ConfirmCallCount++;
            return Task.FromResult(ConfirmResult);
        }
    }

    private sealed class ThrowingInterpolateAllCommandHandler : InterpolateAllCommandHandler
    {
        public ThrowingInterpolateAllCommandHandler(
            IEditorDocumentManager editorDocumentManager,
            IDialogManager dialogManager)
            : base(editorDocumentManager, dialogManager)
        {
        }

        protected override bool Process(FumenVisualEditorViewModel editor, bool xGridLimit) =>
            throw new InvalidOperationException("Test failure");
    }
}
