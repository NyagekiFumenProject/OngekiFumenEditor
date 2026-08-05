using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Gekimini.Avalonia.Modules.Window.Views;
using Gekimini.Avalonia.Platforms.Services.Window;
using Iciclecreek.Avalonia.WindowManager;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.UI;

public sealed class WindowDialogResultTests
{
    [AvaloniaFact]
    public async Task ManagedWindow_BackgroundResizeAnimation_CompletesWithExpectedBounds()
    {
        var windowsPanel = new WindowsPanel();
        var host = new Window
        {
            Width = 640,
            Height = 480,
            Content = windowsPanel
        };
        var managedWindow = new ResizeAnimationTestWindow
        {
            Width = 200,
            Height = 120,
            AnimateWindow = false
        };

        try
        {
            host.Show();
            host.UpdateLayout();
            managedWindow.Show(windowsPanel);
            host.UpdateLayout();

            managedWindow.AnimateWindow = true;
            var oldBounds = new Rect(10, 20, 200, 120);
            var newBounds = new Rect(30, 40, 300, 180);

            await Task.Run(() => managedWindow.RunResizeAnimationAsync(oldBounds, newBounds));

            Assert.Equal(newBounds.X, Canvas.GetLeft(managedWindow));
            Assert.Equal(newBounds.Y, Canvas.GetTop(managedWindow));
            Assert.Equal(newBounds.Width, managedWindow.Width);
            Assert.Equal(newBounds.Height, managedWindow.Height);
        }
        finally
        {
            managedWindow.AnimateWindow = false;
            if (managedWindow.IsVisible)
                managedWindow.Close();
            host.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(WindowState.Maximized)]
    [InlineData(WindowState.Normal)]
    [InlineData(WindowState.Minimized)]
    public async Task ManagedWindow_BackgroundStateTransition_RunsOnUiDispatcher(WindowState windowState)
    {
        var windowsPanel = new WindowsPanel();
        var host = new Window
        {
            Width = 640,
            Height = 480,
            Content = windowsPanel
        };
        var managedWindow = new StateTransitionTestWindow
        {
            Width = 200,
            Height = 120,
            AnimateWindow = false
        };

        try
        {
            host.Show();
            host.UpdateLayout();
            managedWindow.Show(windowsPanel);
            host.UpdateLayout();

            await Task.Run(() => managedWindow.RunStateTransition(windowState));
            var executionContext = await managedWindow.ResizeExecutionContext
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(executionContext.HasDispatcherAccess);
            Assert.True(executionContext.HasSynchronizationContext);
        }
        finally
        {
            if (managedWindow.IsVisible)
                managedWindow.Close();
            host.Close();
        }
    }

    [AvaloniaFact]
    public async Task ManagedWindow_DirectBackgroundShow_FailsFast()
    {
        using var host = new WindowHostScope();
        var managedWindow = new WindowViewBase
        {
            Width = 200,
            Height = 120,
            AnimateWindow = false
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Task.Run(() => managedWindow.Show(host.WindowsPanel)));

        Assert.DoesNotContain(managedWindow, host.WindowsPanel.Windows);
    }

    [AvaloniaFact]
    public async Task ManagedWindow_CloseWithoutSynchronizationContext_CompletesOnUiDispatcher()
    {
        var windowsPanel = new WindowsPanel();
        var host = new Window
        {
            Width = 640,
            Height = 480,
            Content = windowsPanel
        };
        var managedWindow = new ThreadPoolCloseContinuationWindow
        {
            Width = 200,
            Height = 120,
            AnimateWindow = false
        };

        try
        {
            host.Show();
            host.UpdateLayout();
            managedWindow.Show(windowsPanel);
            host.UpdateLayout();

            Task closeTask;
            var previousSynchronizationContext = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                closeTask = managedWindow.CloseAsync();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousSynchronizationContext);
            }

            await closeTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(managedWindow.ClosedHasDispatcherAccess);
            Assert.Equal(1, managedWindow.ClosedCallCount);
            Assert.DoesNotContain(managedWindow, windowsPanel.Windows);
        }
        finally
        {
            if (managedWindow.IsVisible)
                managedWindow.Close();
            host.Close();
        }
    }

    [AvaloniaFact]
    public async Task DefaultWindowManager_BackgroundShowAndClose_RunsLifecycleOnUiDispatcher()
    {
        using var host = new WindowHostScope();
        var windowManager = GetWindowManager();
        var managedWindow = new DispatcherProbeWindow
        {
            Width = 200,
            Height = 120,
            AnimateWindow = false
        };

        await Task.Run(() => windowManager.ShowWindowAsync(managedWindow));

        Assert.True(managedWindow.ShowHasDispatcherAccess);
        Assert.Contains(managedWindow, host.WindowsPanel.Windows);

        await Task.Run(() => windowManager.TryCloseWindowAsync(managedWindow, true));

        Assert.True(managedWindow.CloseHasDispatcherAccess);
        Assert.True(managedWindow.ClosedHasDispatcherAccess);
        Assert.Equal(1, managedWindow.CloseAnimationCallCount);
        Assert.Equal(1, managedWindow.ClosedCallCount);
        Assert.DoesNotContain(managedWindow, host.WindowsPanel.Windows);
    }

    [AvaloniaFact]
    public async Task DefaultWindowManager_BackgroundClose_WaitsForAnimationAndDeduplicatesClose()
    {
        using var host = new WindowHostScope();
        var windowManager = GetWindowManager();
        var managedWindow = new ControlledCloseWindow
        {
            Width = 200,
            Height = 120,
            AnimateWindow = false
        };

        await windowManager.ShowWindowAsync(managedWindow);

        var firstClose = Task.Run(() => windowManager.TryCloseWindowAsync(managedWindow, true));
        await managedWindow.CloseAnimationStarted.WaitAsync(TimeSpan.FromSeconds(5));
        var secondClose = windowManager.TryCloseWindowAsync(managedWindow, true);

        Assert.False(firstClose.IsCompleted);
        Assert.False(secondClose.IsCompleted);
        Assert.Contains(managedWindow, host.WindowsPanel.Windows);

        managedWindow.CompleteCloseAnimation();
        await Task.WhenAll(firstClose, secondClose);

        Assert.Equal(1, managedWindow.CloseAnimationCallCount);
        Assert.Equal(1, managedWindow.ClosedCallCount);
        Assert.True(managedWindow.CloseHasDispatcherAccess);
        Assert.True(managedWindow.ClosedHasDispatcherAccess);
        Assert.DoesNotContain(managedWindow, host.WindowsPanel.Windows);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DefaultWindowManager_BackgroundDialog_PreservesResult(bool expectedResult)
    {
        using var host = new WindowHostScope();
        var windowManager = GetWindowManager();
        var dialog = new DispatcherProbeWindow
        {
            Width = 200,
            Height = 120,
            AnimateWindow = false
        };

        var resultTask = Task.Run(() => windowManager.ShowDialogAsync(dialog));
        await dialog.ShowCompleted.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(resultTask.IsCompleted);
        Assert.True(dialog.ShowHasDispatcherAccess);
        Assert.Same(dialog, host.WindowsPanel.ModalDialog);

        await Task.Run(() => windowManager.TryCloseWindowAsync(dialog, expectedResult));

        Assert.Equal(expectedResult, await resultTask);
        Assert.True(dialog.CloseHasDispatcherAccess);
        Assert.True(dialog.ClosedHasDispatcherAccess);
        Assert.Null(host.WindowsPanel.ModalDialog);
    }

    [AvaloniaFact]
    public async Task DefaultWindowManager_DuplicateDialogClose_PreservesFirstResult()
    {
        using var host = new WindowHostScope();
        var windowManager = GetWindowManager();
        var dialog = new ControlledCloseWindow
        {
            Width = 200,
            Height = 120,
            AnimateWindow = false
        };

        var resultTask = windowManager.ShowDialogAsync(dialog);
        var firstClose = Task.Run(() => windowManager.TryCloseWindowAsync(dialog, true));
        await dialog.CloseAnimationStarted.WaitAsync(TimeSpan.FromSeconds(5));
        var duplicateClose = Task.Run(() => windowManager.TryCloseWindowAsync(dialog, false));

        dialog.CompleteCloseAnimation();
        await Task.WhenAll(firstClose, duplicateClose);

        Assert.True(await resultTask);
        Assert.Equal(1, dialog.CloseAnimationCallCount);
        Assert.Equal(1, dialog.ClosedCallCount);
    }

    [AvaloniaFact]
    public async Task DefaultWindowManager_CancelledDialogClose_CanRetryWithNewResult()
    {
        using var host = new WindowHostScope();
        var windowManager = GetWindowManager();
        var dialog = new DispatcherProbeWindow
        {
            Width = 200,
            Height = 120,
            AnimateWindow = false
        };
        var cancelNextClose = true;
        dialog.Closing += (_, args) =>
        {
            args.Cancel = cancelNextClose;
            cancelNextClose = false;
        };

        var resultTask = windowManager.ShowDialogAsync(dialog);
        await Task.Run(() => windowManager.TryCloseWindowAsync(dialog, true));

        Assert.False(resultTask.IsCompleted);
        Assert.Contains(dialog, host.WindowsPanel.Windows);
        Assert.Same(dialog, host.WindowsPanel.ModalDialog);
        Assert.Equal(0, dialog.CloseAnimationCallCount);

        await Task.Run(() => windowManager.TryCloseWindowAsync(dialog, false));

        Assert.False(await resultTask);
        Assert.Equal(1, dialog.CloseAnimationCallCount);
        Assert.Equal(1, dialog.ClosedCallCount);
        Assert.DoesNotContain(dialog, host.WindowsPanel.Windows);
        Assert.Null(host.WindowsPanel.ModalDialog);
    }

    [Fact]
    public void WindowManager_DialogOverloadsExposeNullableBooleanResult()
    {
        var viewOverload = typeof(IWindowManager).GetMethod(
            nameof(IWindowManager.ShowDialogAsync),
            [typeof(WindowViewBase)]);

        Assert.NotNull(viewOverload);
        Assert.Equal(typeof(Task<bool?>), viewOverload!.ReturnType);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public async Task ManagedWindow_DialogCompletionPreservesNullableBooleanResult(bool? expectedResult)
    {
        var windowsPanel = new WindowsPanel();
        var owner = new Window
        {
            Width = 320,
            Height = 240,
            Content = windowsPanel
        };
        var dialog = new WindowViewBase
        {
            Width = 200,
            Height = 120
        };

        try
        {
            owner.Show();
            owner.UpdateLayout();

            var resultTask = dialog.ShowDialog<bool?>(windowsPanel);
            Assert.False(resultTask.IsCompleted);

            if (expectedResult is { } result)
                dialog.Close(result);
            else
                dialog.Close();

            Assert.Equal(expectedResult, await resultTask);
        }
        finally
        {
            if (dialog.IsVisible)
                dialog.Close();
            owner.Close();
        }
    }

    [AvaloniaFact]
    public async Task ManagedWindow_OwnedDialogStaysAboveOwnerAndUsesOwnerModalState()
    {
        var windowsPanel = new WindowsPanel();
        var host = new Window
        {
            Width = 640,
            Height = 480,
            Content = windowsPanel
        };
        var owner = new WindowViewBase
        {
            Width = 400,
            Height = 300
        };
        var dialog = new WindowViewBase
        {
            Width = 240,
            Height = 160
        };

        try
        {
            host.Show();
            host.UpdateLayout();
            owner.Show(windowsPanel);
            host.UpdateLayout();

            var resultTask = dialog.ShowDialog<bool?>(owner);
            host.UpdateLayout();

            Assert.Same(owner, dialog.Owner);
            Assert.Same(dialog, owner.ModalDialog);
            Assert.Null(windowsPanel.ModalDialog);
            Assert.True(dialog.ZIndex > owner.ZIndex);

            dialog.Close(false);
            Assert.Equal(false, await resultTask);
        }
        finally
        {
            if (dialog.IsVisible)
                dialog.Close();
            if (owner.IsVisible)
                owner.Close();
            host.Close();
        }
    }

    private sealed class ResizeAnimationTestWindow : ManagedWindow
    {
        public Task RunResizeAnimationAsync(Rect oldBounds, Rect newBounds) =>
            ResizeAnimation(oldBounds, newBounds);
    }

    private sealed class StateTransitionTestWindow : ManagedWindow
    {
        private readonly TaskCompletionSource<ExecutionContextState> resizeExecutionContext =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ExecutionContextState> ResizeExecutionContext => resizeExecutionContext.Task;

        public void RunStateTransition(WindowState windowState)
        {
            switch (windowState)
            {
                case WindowState.Maximized:
                    OnMaximizeWindow();
                    break;
                case WindowState.Normal:
                    OnNormalWindow();
                    break;
                case WindowState.Minimized:
                    OnMinimizeWindow();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(windowState), windowState, null);
            }
        }

        protected override Task ResizeAnimation(Rect oldPosition, Rect newPosition)
        {
            resizeExecutionContext.TrySetResult(new ExecutionContextState(
                Dispatcher.UIThread.CheckAccess(),
                SynchronizationContext.Current is not null));
            return Task.CompletedTask;
        }
    }

    private readonly record struct ExecutionContextState(
        bool HasDispatcherAccess,
        bool HasSynchronizationContext);

    private static IWindowManager GetWindowManager()
    {
        var application = Assert.IsAssignableFrom<global::Gekimini.Avalonia.App>(Application.Current);
        return application.ServiceProvider.GetRequiredService<IWindowManager>();
    }

    private sealed class DispatcherProbeWindow : WindowViewBase
    {
        private readonly TaskCompletionSource<bool> showCompleted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ShowHasDispatcherAccess { get; private set; }
        public bool CloseHasDispatcherAccess { get; private set; }
        public bool ClosedHasDispatcherAccess { get; private set; }
        public int CloseAnimationCallCount { get; private set; }
        public int ClosedCallCount { get; private set; }
        public Task ShowCompleted => showCompleted.Task;

        public DispatcherProbeWindow()
        {
            Closed += (_, _) =>
            {
                ClosedHasDispatcherAccess = Dispatcher.UIThread.CheckAccess();
                ClosedCallCount++;
            };
        }

        public override void Show(Visual? parent)
        {
            ShowHasDispatcherAccess = Dispatcher.UIThread.CheckAccess();
            base.Show(parent);
            showCompleted.TrySetResult(true);
        }

        protected override Task CloseAnimation()
        {
            CloseHasDispatcherAccess = Dispatcher.UIThread.CheckAccess();
            CloseAnimationCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThreadPoolCloseContinuationWindow : WindowViewBase
    {
        public bool ClosedHasDispatcherAccess { get; private set; }
        public int ClosedCallCount { get; private set; }

        public ThreadPoolCloseContinuationWindow()
        {
            Closed += (_, _) =>
            {
                ClosedHasDispatcherAccess = Dispatcher.UIThread.CheckAccess();
                ClosedCallCount++;
            };
        }

        protected override async Task CloseAnimation()
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ThreadPool.QueueUserWorkItem(_ => completion.TrySetResult(true));
            await completion.Task;
        }
    }

    private sealed class ControlledCloseWindow : WindowViewBase
    {
        private readonly TaskCompletionSource<bool> closeAnimationStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> completeCloseAnimation =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool CloseHasDispatcherAccess { get; private set; }
        public bool ClosedHasDispatcherAccess { get; private set; }
        public int CloseAnimationCallCount { get; private set; }
        public int ClosedCallCount { get; private set; }
        public Task CloseAnimationStarted => closeAnimationStarted.Task;

        public ControlledCloseWindow()
        {
            Closed += (_, _) =>
            {
                ClosedHasDispatcherAccess = Dispatcher.UIThread.CheckAccess();
                ClosedCallCount++;
            };
        }

        public void CompleteCloseAnimation()
        {
            completeCloseAnimation.TrySetResult(true);
        }

        protected override async Task CloseAnimation()
        {
            CloseHasDispatcherAccess = Dispatcher.UIThread.CheckAccess();
            CloseAnimationCallCount++;
            closeAnimationStarted.TrySetResult(true);
            await completeCloseAnimation.Task;
        }
    }

    private sealed class WindowHostScope : IDisposable
    {
        private readonly Window host;
        private readonly IClassicDesktopStyleApplicationLifetime? desktopLifetime;
        private readonly ISingleViewApplicationLifetime? singleViewLifetime;
        private readonly Window? previousMainWindow;
        private readonly Control? previousMainView;

        public WindowsPanel WindowsPanel { get; } = new();

        public WindowHostScope()
        {
            host = new Window
            {
                Width = 640,
                Height = 480,
                Content = WindowsPanel
            };

            switch (Application.Current?.ApplicationLifetime)
            {
                case IClassicDesktopStyleApplicationLifetime desktop:
                    desktopLifetime = desktop;
                    previousMainWindow = desktop.MainWindow;
                    desktop.MainWindow = host;
                    break;
                case ISingleViewApplicationLifetime singleView:
                    singleViewLifetime = singleView;
                    previousMainView = singleView.MainView;
                    singleView.MainView = host;
                    break;
                default:
                    throw new InvalidOperationException("The Avalonia test application has no supported lifetime.");
            }

            host.Show();
            host.UpdateLayout();
        }

        public void Dispose()
        {
            if (desktopLifetime is not null)
                desktopLifetime.MainWindow = previousMainWindow;
            if (singleViewLifetime is not null)
                singleViewLifetime.MainView = previousMainView;

            host.Close();
        }
    }
}
