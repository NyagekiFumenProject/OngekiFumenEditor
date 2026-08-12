using System.ComponentModel;
using System.Reflection;
using Avalonia.Headless.XUnit;
using Gekimini.Avalonia.Modules.Window.ViewModels;
using Gekimini.Avalonia.Modules.Window.Views;
using Gekimini.Avalonia.Platforms.Services.Window;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels.Dialogs;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.UI;

public sealed class BulletPalleteSelectionTests
{
    [AvaloniaFact]
    public async Task ChangedSelectionThenCancel_DoesNotWriteProperty()
    {
        var (viewModel, proxy, initial, replacement) = CreateContext();
        var windowManager = new RecordingWindowManager
        {
            Result = false,
            Interact = async dialog =>
            {
                dialog.SelectedPallete = replacement;
                await dialog.CancelCommand.ExecuteAsync(null);
            }
        };

        var changed = await viewModel.OpenSelectListCoreAsync(
            [initial, replacement],
            windowManager);

        Assert.False(changed);
        Assert.Same(initial, proxy.Value);
        Assert.Equal(0, proxy.SetCallCount);
    }

    [AvaloniaFact]
    public async Task ChangedSelectionThenWindowClose_DoesNotWriteProperty()
    {
        var (viewModel, proxy, initial, replacement) = CreateContext();
        var windowManager = new RecordingWindowManager
        {
            Result = null,
            Interact = dialog =>
            {
                dialog.SelectedPallete = replacement;
                return Task.CompletedTask;
            }
        };

        var changed = await viewModel.OpenSelectListCoreAsync(
            [initial, replacement],
            windowManager);

        Assert.False(changed);
        Assert.Same(initial, proxy.Value);
        Assert.Equal(0, proxy.SetCallCount);
    }

    [AvaloniaFact]
    public async Task ConfirmedSelection_WritesPropertyOnce()
    {
        var (viewModel, proxy, initial, replacement) = CreateContext();
        var strIdNotificationCount = 0;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BulletPalleteTypeUIViewModel.StrId))
                strIdNotificationCount++;
        };
        var windowManager = new RecordingWindowManager
        {
            Result = true,
            Interact = async dialog =>
            {
                dialog.SelectedPallete = replacement;
                await dialog.ConfirmCommand.ExecuteAsync(null);
            }
        };

        var changed = await viewModel.OpenSelectListCoreAsync(
            [initial, replacement],
            windowManager);

        Assert.True(changed);
        Assert.Same(replacement, proxy.Value);
        Assert.Equal(1, proxy.SetCallCount);
        Assert.Equal(1, strIdNotificationCount);
    }

    [AvaloniaFact]
    public async Task DoubleTapConfirmation_WritesSelectedPropertyOnce()
    {
        var (viewModel, proxy, initial, replacement) = CreateContext();
        var windowManager = new RecordingWindowManager
        {
            Result = true,
            Interact = dialog => dialog.SelectAndConfirmCommand.ExecuteAsync(replacement)
        };

        var changed = await viewModel.OpenSelectListCoreAsync(
            [initial, replacement],
            windowManager);

        Assert.True(changed);
        Assert.Same(replacement, proxy.Value);
        Assert.Equal(1, proxy.SetCallCount);
    }

    [AvaloniaFact]
    public async Task ConfirmedUnchangedSelection_DoesNotWriteProperty()
    {
        var (viewModel, proxy, initial, replacement) = CreateContext();
        var windowManager = new RecordingWindowManager
        {
            Result = true,
            Interact = dialog => dialog.ConfirmCommand.ExecuteAsync(null)
        };

        var changed = await viewModel.OpenSelectListCoreAsync(
            [initial, replacement],
            windowManager);

        Assert.False(changed);
        Assert.Same(initial, proxy.Value);
        Assert.Equal(0, proxy.SetCallCount);
    }

    private static (BulletPalleteTypeUIViewModel ViewModel, RecordingPropertyProxy Proxy,
        BulletPallete Initial, BulletPallete Replacement) CreateContext()
    {
        var initial = new BulletPallete { StrID = "BPL_A" };
        var replacement = new BulletPallete { StrID = "BPL_B" };
        var proxy = new RecordingPropertyProxy(initial);
        return (new BulletPalleteTypeUIViewModel(proxy), proxy, initial, replacement);
    }

    private sealed class RecordingWindowManager : IWindowManager
    {
        public bool? Result { get; init; }

        public Func<BulletPalleteSelectDialogViewModel, Task>? Interact { get; init; }

        public WindowViewBase FindExistingWindow(WindowViewModelBase windowViewModel) => null!;

        public Task ShowWindowAsync(WindowViewBase windowView) => Task.CompletedTask;

        public Task<bool?> ShowDialogAsync(WindowViewBase windowView) => Task.FromResult(Result);

        public Task TryCloseWindowAsync(WindowViewBase windowView, bool dialogResult) => Task.CompletedTask;

        public Task ShowWindowAsync(WindowViewModelBase windowViewModel) => Task.CompletedTask;

        public async Task<bool?> ShowDialogAsync(WindowViewModelBase windowViewModel)
        {
            var dialog = Assert.IsType<BulletPalleteSelectDialogViewModel>(windowViewModel);
            if (Interact is not null)
                await Interact(dialog);
            return Result;
        }

        public Task TryCloseWindowAsync(WindowViewModelBase windowViewModelBase, bool dialogResult) =>
            Task.CompletedTask;
    }

    private sealed class RecordingPropertyProxy : IObjectPropertyAccessProxy
    {
        private BulletPallete value;

        public RecordingPropertyProxy(BulletPallete value)
        {
            this.value = value;
        }

        public BulletPallete Value => value;

        public int SetCallCount { get; private set; }

        public PropertyInfo PropertyInfo { get; } = typeof(PropertyOwner)
            .GetProperty(nameof(PropertyOwner.Value))!;

        public object ProxyValue
        {
            get => value;
            set
            {
                this.value = Assert.IsType<BulletPallete>(value);
                SetCallCount++;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProxyValue)));
            }
        }

        public string DisplayPropertyName => nameof(PropertyOwner.Value);

        public string DisplayPropertyTipText => string.Empty;

        public bool IsAllowSetNull => true;

        public bool IsReadOnly => false;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Clear()
        {
        }
    }

    private sealed class PropertyOwner
    {
        public BulletPallete? Value { get; set; }
    }
}
