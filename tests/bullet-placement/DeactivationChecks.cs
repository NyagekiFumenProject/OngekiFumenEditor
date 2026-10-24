using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Caliburn.Micro;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;

/// <summary>
/// 仅用于测试的隐藏窗口。直接调用受保护的 <see cref="Window.OnDeactivated"/>，
/// 触发与真实 Alt-Tab 相同的 Window.Deactivated 路由，但不显示、不激活窗口。
/// </summary>
internal sealed class ProbeWindow : Window
{
    public void RaiseDeactivated() => OnDeactivated(EventArgs.Empty);
}

/// <summary>
/// 固定 Window.Deactivated 与 Caliburn 文档失活各自的语义，避免用 OnDeactivateAsync
/// 替换窗口失焦处理时把文档生命周期副作用带入失焦路径。
/// </summary>
internal static class DeactivationChecks
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const int SoflanGroupId = 7;

    public static void Run(Action<bool, string> check)
    {
        var previousWindow = Application.Current.MainWindow;
        var window = new ProbeWindow { ShowInTaskbar = false, ShowActivated = false };
        Application.Current.MainWindow = window;
        var failures = new List<string>();
        void IsolatedCheck(bool condition, string label)
        {
            try
            {
                check(condition, label);
            }
            catch (Exception error)
            {
                failures.Add(label + ": " + error.Message);
            }
        }
        void Scenario(string name, System.Action action)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                failures.Add(name + " threw: " + error);
            }
        }
        try
        {
            Scenario("window deactivation cancels ISA", () => WindowDeactivationCancelsIsa(IsolatedCheck, window));
            Scenario("window deactivation aborts selection drag", () => WindowDeactivationAbortsSelectionDrag(IsolatedCheck, window));
            Scenario("window deactivation keeps lifecycle and batch placement",
                () => WindowDeactivationKeepsDocumentLifecycleAndBatchPlacement(IsolatedCheck, window));
            Scenario("document deactivation cleans sessions", () => DocumentDeactivationCleansSessions(IsolatedCheck, window));
            Scenario("shell document switch deactivates old editor", () => ShellDocumentSwitchDeactivatesOldEditor(IsolatedCheck, window));
            Scenario("close path is idempotent and window-safe", () => ClosePathIsIdempotentAndWindowSafe(IsolatedCheck, window));
            Scenario("repeated activation does not duplicate handlers",
                () => RepeatedDeactivationAndActivationDoNotDuplicateHandlers(IsolatedCheck, window));
        }
        finally
        {
            Application.Current.MainWindow = previousWindow;
        }
        if (failures.Count > 0)
            throw new InvalidOperationException(
                $"Deactivation behavior failures ({failures.Count}):{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    private static void WindowDeactivationCancelsIsa(Action<bool, string> check, ProbeWindow window)
    {
        foreach (var expectedPhase in new[] { IsaPlacementPhase.WaitingFirstPoint, IsaPlacementPhase.WaitingSecondPoint })
        {
            var editor = CheckSupport.CreateEditor();
            try
            {
                CheckSupport.Pump();
                check(editor.IsActive, $"{expectedPhase}: editor is active");
                check(ReferenceEquals(WindowHook(editor), window), $"{expectedPhase}: editor hooked the application window");
                editor.Fumen.IndividualSoflanAreaMap.SoflanGroupTree.GetOrCreateGroup(SoflanGroupId);
                check(editor.TryStartIsaPlacement(SoflanGroupId), $"{expectedPhase}: ISA placement starts");
                check(editor.IsIsaPlacementActive && editor.IsaPlacementPhase == IsaPlacementPhase.WaitingFirstPoint &&
                    editor.CurrentCursor is not null, $"{expectedPhase}: placement waits for the first point with cross cursor");

                if (expectedPhase == IsaPlacementPhase.WaitingSecondPoint)
                {
                    check(editor.TryTakeIsaPlacementPoint(new Point(400, 400)), $"{expectedPhase}: first point accepted");
                    check(editor.IsaPlacementPhase == IsaPlacementPhase.WaitingSecondPoint,
                        $"{expectedPhase}: placement waits for the second point");
                }

                var history = editor.UndoRedoManager.UndoActionCount;
                var isaCount = CountIsa(editor);
                window.RaiseDeactivated();
                check(!editor.IsIsaPlacementActive && editor.IsaPlacementPhase == IsaPlacementPhase.Inactive,
                    $"{expectedPhase}: window deactivation cancels ISA placement");
                check(editor.CurrentCursor is null, $"{expectedPhase}: cross cursor is cleared");
                check(editor.IsaPlacementTargetSoflanGroupId == 0, $"{expectedPhase}: target group is reset");

                check(!editor.TryTakeIsaPlacementPoint(new Point(500, 300)),
                    $"{expectedPhase}: late point after window deactivation is rejected");
                check(CountIsa(editor) == isaCount && editor.UndoRedoManager.UndoActionCount == history,
                    $"{expectedPhase}: late point creates no ISA or undo history");
            }
            finally
            {
                Cleanup(editor);
            }
        }
    }

    private static void WindowDeactivationAbortsSelectionDrag(Action<bool, string> check, ProbeWindow window)
    {
        var editor = CheckSupport.CreateEditor();
        try
        {
            CheckSupport.Pump();
            var tap = new Tap { TGrid = TGrid.FromTotalGrid(173), XGrid = new XGrid(0) };
            editor.Fumen.AddObject(tap);
            editor.IsDirty = false;
            var action = editor.InteractiveManager.GetInteractive(tap);
            var start = new Point(XGridCalculator.ConvertXGridToX(tap.XGrid, editor),
                editor.ConvertTGridToY_DesignMode(tap.TGrid));
            action.OnDragStart(tap, start, editor);
            check(editor.InteractiveManager.IsSelectionDragging, "real selection drag transaction started");
            action.OnDragMove(tap, new Point(start.X + 60, start.Y + 120), editor);
            check(tap.TGrid.TotalGrid != 173, "drag moved the object before window deactivation");
            editor.IsDirty = true;
            var history = editor.UndoRedoManager.UndoActionCount;

            window.RaiseDeactivated();
            check(!editor.InteractiveManager.IsSelectionDragging, "window deactivation aborts the drag transaction");
            check(tap.TGrid.TotalGrid == 173, "window deactivation restores the dragged T coordinate");
            check(!editor.IsDirty, "window deactivation restores the pre-drag dirty state");
            check(editor.UndoRedoManager.UndoActionCount == history, "aborted drag adds no undo action");
            check(!(bool)Field(editor, "isObjectDragStarted"), "drag-start input flag is reset");
            check((int)Field(editor, "autoScrollRedispatchToken") == int.MaxValue,
                "pending auto-scroll redispatch is invalidated");
        }
        finally
        {
            Cleanup(editor);
        }
    }

    private static void WindowDeactivationKeepsDocumentLifecycleAndBatchPlacement(Action<bool, string> check, ProbeWindow window)
    {
        var editor = CheckSupport.CreateEditor();
        try
        {
            CheckSupport.Pump();
            var pallete = new BulletPallete();
            editor.Fumen.AddObject(pallete);
            editor.ToggleBatchProjectilePlacement(pallete, false);
            var request = editor.RequestBatchPlaceProjectileInfo;
            check(request is not null && editor.IsRequestPlaceBatchProjectilePreview, "batch placement session started");
            var attempts = 0;
            var deactivations = 0;
            editor.AttemptingDeactivation += (_, _) => attempts++;
            editor.Deactivated += (_, _) =>
            {
                deactivations++;
                return Task.CompletedTask;
            };

            window.RaiseDeactivated();
            check(editor.IsActive, "window deactivation keeps the document active");
            check(attempts == 0 && deactivations == 0, "window deactivation does not enter document lifecycle");
            check(ReferenceEquals(request, editor.RequestBatchPlaceProjectileInfo),
                "window deactivation does not cancel the batch placement session");
        }
        finally
        {
            Cleanup(editor);
        }
    }

    private static void DocumentDeactivationCleansSessions(Action<bool, string> check, ProbeWindow window)
    {
        var editor = CheckSupport.CreateEditor();
        try
        {
            CheckSupport.Pump();
            var attempts = 0;
            var deactivations = 0;
            editor.AttemptingDeactivation += (_, e) =>
            {
                attempts++;
                check(!e.WasClosed, "non-close deactivation reports WasClosed=false");
            };
            editor.Deactivated += (_, e) =>
            {
                deactivations++;
                check(!e.WasClosed, "non-close deactivation completes with WasClosed=false");
                return Task.CompletedTask;
            };

            editor.Fumen.IndividualSoflanAreaMap.SoflanGroupTree.GetOrCreateGroup(SoflanGroupId);
            check(editor.TryStartIsaPlacement(SoflanGroupId), "document deactivation scenario starts ISA placement");
            Deactivate(editor);
            check(!editor.IsActive && !editor.IsIsaPlacementActive, "document deactivation cancels ISA placement");
            check(editor.CurrentCursor is null, "document deactivation clears the cross cursor");
            check(attempts == 1 && deactivations == 1, "document deactivation raises each lifecycle event once");

            Deactivate(editor);
            check(attempts == 1 && deactivations == 1, "repeated deactivation does not re-enter the lifecycle");

            Activate(editor);
            check(editor.IsActive, "editor reactivates for the drag scenario");
            var tap = new Tap { TGrid = TGrid.FromTotalGrid(173), XGrid = new XGrid(0) };
            editor.Fumen.AddObject(tap);
            var action = editor.InteractiveManager.GetInteractive(tap);
            var start = new Point(XGridCalculator.ConvertXGridToX(tap.XGrid, editor),
                editor.ConvertTGridToY_DesignMode(tap.TGrid));
            action.OnDragStart(tap, start, editor);
            action.OnDragMove(tap, new Point(start.X + 60, start.Y + 120), editor);
            check(editor.InteractiveManager.IsSelectionDragging, "drag transaction is active before document deactivation");
            Deactivate(editor);
            check(!editor.InteractiveManager.IsSelectionDragging && tap.TGrid.TotalGrid == 173,
                "document deactivation aborts and rolls back the drag transaction");
            check(attempts == 2 && deactivations == 2, "second deactivation raises each lifecycle event once");

            Activate(editor);
            var pallete = new BulletPallete();
            editor.Fumen.AddObject(pallete);
            editor.ToggleBatchProjectilePlacement(pallete, false);
            check(editor.RequestBatchPlaceProjectileInfo is not null, "batch placement is active before document deactivation");
            Deactivate(editor);
            check(editor.RequestBatchPlaceProjectileInfo is null && !editor.IsRequestPlaceBatchProjectilePreview,
                "document deactivation cancels batch placement");
            check(attempts == 3 && deactivations == 3, "third deactivation raises each lifecycle event once");
        }
        finally
        {
            Cleanup(editor);
        }
    }

    private static void ShellDocumentSwitchDeactivatesOldEditor(Action<bool, string> check, ProbeWindow window)
    {
        var first = CheckSupport.CreateEditor();
        try
        {
            CheckSupport.Pump();
            first.Fumen.IndividualSoflanAreaMap.SoflanGroupTree.GetOrCreateGroup(SoflanGroupId);
            check(first.TryStartIsaPlacement(SoflanGroupId), "old document starts ISA placement");

            var second = CheckSupport.CreateEditor();
            try
            {
                CheckSupport.Pump();
                check(!first.IsActive, "document switch deactivates the old editor");
                check(!first.IsIsaPlacementActive && first.IsaPlacementPhase == IsaPlacementPhase.Inactive,
                    "document switch cleans the old ISA session");
                check(ReferenceEquals(IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor, second),
                    "document switch activates the new editor");

                IoC.Get<IShell>().ActiveLayoutItem = first;
                CheckSupport.Pump();
                check(first.IsActive, "old editor can be reactivated");
                check(!first.IsIsaPlacementActive, "old ISA session does not resurrect on reactivation");
                check(first.TryStartIsaPlacement(SoflanGroupId), "placement can start again after reactivation");
                window.RaiseDeactivated();
                check(!first.IsIsaPlacementActive, "reactivated editor is hooked to window deactivation again");
            }
            finally
            {
                Cleanup(second);
            }
        }
        finally
        {
            Cleanup(first);
        }
    }

    private static void ClosePathIsIdempotentAndWindowSafe(Action<bool, string> check, ProbeWindow window)
    {
        var editor = CheckSupport.CreateEditor();
        try
        {
            CheckSupport.Pump();
            editor.Fumen.IndividualSoflanAreaMap.SoflanGroupTree.GetOrCreateGroup(SoflanGroupId);
            check(editor.TryStartIsaPlacement(SoflanGroupId), "close scenario starts ISA placement");
            var attempts = 0;
            editor.AttemptingDeactivation += (_, _) => attempts++;

            ((IDeactivate)editor).DeactivateAsync(true, CancellationToken.None).GetAwaiter().GetResult();
            check(!editor.IsActive && !editor.IsIsaPlacementActive, "close deactivates and cancels the ISA session");
            check(attempts == 1, "close raises AttemptingDeactivation once");

            check(NoThrow(window.RaiseDeactivated), "window deactivation after close is safe");
            check(!editor.IsIsaPlacementActive, "window deactivation after close changes nothing");
            check(NoThrow(() => ((IDeactivate)editor).DeactivateAsync(true, CancellationToken.None).GetAwaiter().GetResult()),
                "repeated close is safe");
        }
        finally
        {
            editor.Dispose();
        }
    }

    private static void RepeatedDeactivationAndActivationDoNotDuplicateHandlers(Action<bool, string> check, ProbeWindow window)
    {
        var editor = CheckSupport.CreateEditor();
        try
        {
            CheckSupport.Pump();
            editor.Fumen.IndividualSoflanAreaMap.SoflanGroupTree.GetOrCreateGroup(SoflanGroupId);
            var notifications = 0;
            editor.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(FumenVisualEditorViewModel.IsIsaPlacementActive))
                    notifications++;
            };

            check(editor.TryStartIsaPlacement(SoflanGroupId), "repeated-deactivation scenario starts ISA placement");
            notifications = 0;
            window.RaiseDeactivated();
            check(notifications == 1, "one window deactivation cancels ISA exactly once");
            window.RaiseDeactivated();
            check(notifications == 1, "repeated window deactivation while inactive is a no-op");

            Deactivate(editor);
            Activate(editor);
            Deactivate(editor);
            Activate(editor);
            check(editor.IsActive, "editor is active after repeated deactivate/activate cycles");
            check(editor.TryStartIsaPlacement(SoflanGroupId), "placement restarts after repeated cycles");
            notifications = 0;
            window.RaiseDeactivated();
            check(notifications == 1, "reactivation does not duplicate the window deactivation handler");
        }
        finally
        {
            Cleanup(editor);
        }
    }

    private static void Activate(FumenVisualEditorViewModel editor)
        => ((IActivate)editor).ActivateAsync(CancellationToken.None).GetAwaiter().GetResult();

    private static void Deactivate(FumenVisualEditorViewModel editor)
        => ((IDeactivate)editor).DeactivateAsync(false, CancellationToken.None).GetAwaiter().GetResult();

    private static void Cleanup(FumenVisualEditorViewModel editor)
    {
        try
        {
            if (editor.IsActive)
                Deactivate(editor);
        }
        finally
        {
            editor.Dispose();
        }
    }

    private static int CountIsa(FumenVisualEditorViewModel editor)
        => editor.Fumen.GetAllDisplayableObjects().OfType<IndividualSoflanArea>().Count();

    private static object? Field(FumenVisualEditorViewModel editor, string name)
        => typeof(FumenVisualEditorViewModel).GetField(name, Members)?.GetValue(editor);

    private static object? WindowHook(FumenVisualEditorViewModel editor)
        => Field(editor, "focusedWindow");

    private static bool NoThrow(System.Action action)
    {
        try
        {
            action();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
