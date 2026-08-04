using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Gekimini.Avalonia.Modules.Window.Views;
using Gekimini.Avalonia.Platforms.Services.Window;
using Iciclecreek.Avalonia.WindowManager;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.UI;

public sealed class WindowDialogResultTests
{
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
}
