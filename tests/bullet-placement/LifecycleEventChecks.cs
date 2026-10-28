using System.Reflection;
using System.Windows;
using Caliburn.Micro;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;

/// <summary>
/// 固定 Caliburn <see cref="IDeactivate.AttemptingDeactivation"/> / <see cref="IDeactivate.Deactivated"/>
/// 在真实编辑器上的触发时机、参数与异常语义，区分文档生命周期与窗口失焦。
/// </summary>
internal static class LifecycleEventChecks
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
            Scenario("window deactivation raises no lifecycle events",
                () => WindowDeactivationRaisesNoEvents(IsolatedCheck, window));
            Scenario("document deactivation event order", () => DocumentDeactivationOrder(IsolatedCheck));
            Scenario("document switch raises lifecycle events", () => DocumentSwitchRaisesEvents(IsolatedCheck));
            Scenario("close event order", () => CloseEventOrder(IsolatedCheck));
            Scenario("repeated close event semantics", () => RepeatedCloseEvents(IsolatedCheck));
            Scenario("attempting handler exception blocks deactivation",
                () => AttemptingHandlerException(IsolatedCheck));
            Scenario("deactivated handler exception keeps cleanup", () => DeactivatedHandlerException(IsolatedCheck));
            Scenario("deactivated event awaits handlers", () => DeactivatedWaitsHandlers(IsolatedCheck));
        }
        finally
        {
            Application.Current.MainWindow = previousWindow;
        }
        if (failures.Count > 0)
            throw new InvalidOperationException(
                $"Lifecycle event failures ({failures.Count}):{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    private static void WindowDeactivationRaisesNoEvents(Action<bool, string> check, ProbeWindow window)
    {
        var editor = CheckSupport.CreateEditor();
        try
        {
            CheckSupport.Pump();
            editor.Fumen.IndividualSoflanAreaMap.SoflanGroupTree.GetOrCreateGroup(SoflanGroupId);
            check(editor.TryStartIsaPlacement(SoflanGroupId), "editor starts ISA placement before window deactivation");
            var attempts = 0;
            var deactivations = 0;
            editor.AttemptingDeactivation += (_, _) => attempts++;
            editor.Deactivated += (_, _) =>
            {
                deactivations++;
                return Task.CompletedTask;
            };

            window.RaiseDeactivated();
            check(attempts == 0 && deactivations == 0,
                "Window.Deactivated raises neither AttemptingDeactivation nor Deactivated");
            check(editor.IsActive, "window deactivation keeps the document active");
            check(!editor.IsIsaPlacementActive,
                "window deactivation cancels ISA through the dedicated window path, not lifecycle events");
        }
        finally
        {
            Cleanup(editor);
        }
    }

    private static void DocumentDeactivationOrder(Action<bool, string> check)
    {
        var editor = CheckSupport.CreateEditor();
        try
        {
            CheckSupport.Pump();
            editor.Fumen.IndividualSoflanAreaMap.SoflanGroupTree.GetOrCreateGroup(SoflanGroupId);
            check(editor.TryStartIsaPlacement(SoflanGroupId), "editor starts ISA placement before document deactivation");
            var order = new List<string>();
            var activeAtAttempting = false;
            var activeAtDeactivated = true;
            var isaAtAttempting = false;
            var isaAtDeactivated = true;
            var closedAtAttempting = true;
            var closedAtDeactivated = true;
            editor.AttemptingDeactivation += (_, e) =>
            {
                order.Add("attempting");
                activeAtAttempting = editor.IsActive;
                isaAtAttempting = editor.IsIsaPlacementActive;
                closedAtAttempting = e.WasClosed;
            };
            editor.Deactivated += (_, e) =>
            {
                order.Add("deactivated");
                activeAtDeactivated = editor.IsActive;
                isaAtDeactivated = editor.IsIsaPlacementActive;
                closedAtDeactivated = e.WasClosed;
                return Task.CompletedTask;
            };

            Deactivate(editor);
            check(order.SequenceEqual(new[] { "attempting", "deactivated" }),
                "AttemptingDeactivation runs before Deactivated, each exactly once");
            check(activeAtAttempting && !activeAtDeactivated,
                "IsActive is true while attempting and false when deactivated");
            check(isaAtAttempting && !isaAtDeactivated,
                "AttemptingDeactivation precedes ISA cleanup; Deactivated follows it");
            check(!closedAtAttempting && !closedAtDeactivated, "non-close lifecycle reports WasClosed=false twice");
            check(!editor.IsActive && !editor.IsIsaPlacementActive && editor.CurrentCursor is null,
                "document deactivation leaves no ISA session or cross cursor");
        }
        finally
        {
            Cleanup(editor);
        }
    }

    private static void DocumentSwitchRaisesEvents(Action<bool, string> check)
    {
        var first = CheckSupport.CreateEditor();
        try
        {
            CheckSupport.Pump();
            var firstAttempts = 0;
            var firstDeactivations = 0;
            var firstClosed = true;
            first.AttemptingDeactivation += (_, e) =>
            {
                firstAttempts++;
                firstClosed = e.WasClosed;
            };
            first.Deactivated += (_, _) =>
            {
                firstDeactivations++;
                return Task.CompletedTask;
            };

            var second = CheckSupport.CreateEditor();
            try
            {
                CheckSupport.Pump();
                check(firstAttempts == 1 && firstDeactivations == 1,
                    "document switch raises each lifecycle event once on the old editor");
                check(!firstClosed, "document switch reports WasClosed=false");
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

    private static void CloseEventOrder(Action<bool, string> check)
    {
        var editor = CheckSupport.CreateEditor();
        try
        {
            CheckSupport.Pump();
            editor.Fumen.IndividualSoflanAreaMap.SoflanGroupTree.GetOrCreateGroup(SoflanGroupId);
            check(editor.TryStartIsaPlacement(SoflanGroupId), "editor starts ISA placement before close");
            var order = new List<string>();
            var closedAtAttempting = false;
            var closedAtDeactivated = false;
            var disposedAtDeactivated = false;
            var isaAtDeactivated = true;
            editor.AttemptingDeactivation += (_, e) =>
            {
                order.Add("attempting");
                closedAtAttempting = e.WasClosed;
            };
            editor.Deactivated += (_, e) =>
            {
                order.Add("deactivated");
                closedAtDeactivated = e.WasClosed;
                disposedAtDeactivated = (int)Field(editor, "isDisposed") == 1;
                isaAtDeactivated = editor.IsIsaPlacementActive;
                return Task.CompletedTask;
            };

            Close(editor);
            check(order.SequenceEqual(new[] { "attempting", "deactivated" }),
                "close raises AttemptingDeactivation before Deactivated, each exactly once");
            check(closedAtAttempting && closedAtDeactivated, "close lifecycle reports WasClosed=true twice");
            check(disposedAtDeactivated, "Dispose completes before Deactivated handlers run");
            check(!isaAtDeactivated && !editor.IsActive, "close cleans the ISA session before Deactivated");
        }
        finally
        {
            editor.Dispose();
        }
    }

    private static void RepeatedCloseEvents(Action<bool, string> check)
    {
        var manager = IoC.Get<IEditorDocumentManager>();
        var destroyed = 0;
        void OnDestroyed(FumenVisualEditorViewModel _) => destroyed++;
        manager.OnNotifyDestoryed += OnDestroyed;
        var editor = CheckSupport.CreateEditor();
        try
        {
            CheckSupport.Pump();
            var attempts = 0;
            var deactivations = 0;
            editor.AttemptingDeactivation += (_, _) => attempts++;
            editor.Deactivated += (_, _) =>
            {
                deactivations++;
                return Task.CompletedTask;
            };

            Close(editor);
            check(attempts == 1 && deactivations == 1, "first close raises each lifecycle event once");
            check(destroyed == 1, "first close notifies destruction once");
            check(NoThrow(() => Close(editor)), "second close is safe");
            check(attempts == 2 && deactivations == 2,
                "repeated close re-enters the lifecycle events because IsInitialized stays true");
            check(destroyed == 1, "repeated close does not notify destruction twice");
            check(!editor.IsActive, "editor remains inactive after repeated close");
        }
        finally
        {
            editor.Dispose();
            manager.OnNotifyDestoryed -= OnDestroyed;
        }
    }

    private static void AttemptingHandlerException(Action<bool, string> check)
    {
        var editor = CheckSupport.CreateEditor();
        try
        {
            CheckSupport.Pump();
            editor.Fumen.IndividualSoflanAreaMap.SoflanGroupTree.GetOrCreateGroup(SoflanGroupId);
            check(editor.TryStartIsaPlacement(SoflanGroupId), "editor starts ISA placement before throwing handler");
            var deactivations = 0;
            editor.Deactivated += (_, _) =>
            {
                deactivations++;
                return Task.CompletedTask;
            };
            EventHandler<DeactivationEventArgs> boom = (_, _) => throw new InvalidOperationException("attempting-boom");
            editor.AttemptingDeactivation += boom;

            var threw = false;
            try
            {
                Deactivate(editor);
            }
            catch (InvalidOperationException error) when (error.Message == "attempting-boom")
            {
                threw = true;
            }
            check(threw, "AttemptingDeactivation handler exception propagates to the caller");
            check(editor.IsActive && editor.IsIsaPlacementActive,
                "throwing handler prevents cleanup and the IsActive transition");
            check(deactivations == 0, "throwing handler prevents Deactivated handlers");

            editor.AttemptingDeactivation -= boom;
            Deactivate(editor);
            check(!editor.IsActive && !editor.IsIsaPlacementActive && deactivations == 1,
                "removing the throwing handler lets deactivation complete");
        }
        finally
        {
            Cleanup(editor);
        }
    }

    private static void DeactivatedHandlerException(Action<bool, string> check)
    {
        var editor = CheckSupport.CreateEditor();
        try
        {
            CheckSupport.Pump();
            editor.Fumen.IndividualSoflanAreaMap.SoflanGroupTree.GetOrCreateGroup(SoflanGroupId);
            check(editor.TryStartIsaPlacement(SoflanGroupId), "editor starts ISA placement before throwing handler");
            AsyncEventHandler<DeactivationEventArgs> boom = (_, _) => throw new InvalidOperationException("deactivated-boom");
            editor.Deactivated += boom;

            var threw = false;
            try
            {
                Deactivate(editor);
            }
            catch (InvalidOperationException error) when (error.Message == "deactivated-boom")
            {
                threw = true;
            }
            check(threw, "Deactivated handler exception propagates to the caller");
            check(!editor.IsActive && !editor.IsIsaPlacementActive,
                "cleanup and the IsActive transition already completed before Deactivated handlers");

            editor.Deactivated -= boom;
            Activate(editor);
            check(editor.IsActive, "editor can be reactivated after a throwing Deactivated handler");
            Deactivate(editor);
            check(!editor.IsActive, "deactivation succeeds after removing the throwing handler");
        }
        finally
        {
            Cleanup(editor);
        }
    }

    private static void DeactivatedWaitsHandlers(Action<bool, string> check)
    {
        var editor = CheckSupport.CreateEditor();
        try
        {
            CheckSupport.Pump();
            editor.Fumen.IndividualSoflanAreaMap.SoflanGroupTree.GetOrCreateGroup(SoflanGroupId);
            check(editor.TryStartIsaPlacement(SoflanGroupId), "editor starts ISA placement before asynchronous handler");
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            editor.Deactivated += (_, _) =>
            {
                entered.TrySetResult(true);
                return gate.Task;
            };

            var task = ((IDeactivate)editor).DeactivateAsync(false, CancellationToken.None);
            check(entered.Task.Wait(TimeSpan.FromSeconds(5)), "async Deactivated handler is invoked synchronously during deactivation");
            check(!task.IsCompleted, "DeactivateAsync does not complete before the handler's Task");
            check(!editor.IsActive && !editor.IsIsaPlacementActive,
                "cleanup finishes before awaiting asynchronous Deactivated handlers");

            gate.SetResult(true);
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!task.IsCompleted && DateTime.UtcNow < deadline)
                Thread.Sleep(10);
            check(task.IsCompleted && task.Status == TaskStatus.RanToCompletion,
                "DeactivateAsync completes after the asynchronous handler finishes");
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

    private static void Close(FumenVisualEditorViewModel editor)
        => ((IDeactivate)editor).DeactivateAsync(true, CancellationToken.None).GetAwaiter().GetResult();

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

    private static object? Field(FumenVisualEditorViewModel editor, string name)
        => typeof(FumenVisualEditorViewModel).GetField(name, Members)?.GetValue(editor);

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
