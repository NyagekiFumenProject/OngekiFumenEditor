using Avalonia.Controls;
using OngekiFumenEditor.Avalonia.Desktop.UI.Dialogs;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.UI;

public sealed class ExceptionTermWindowPlacementTests
{
    [Fact]
    public void ExceptionTermWindow_IsDesktopOwnedNativeWindow()
    {
        Assert.Same(typeof(OngekiFumenEditorDesktopApp).Assembly, typeof(ExceptionTermWindow).Assembly);
        Assert.True(typeof(Window).IsAssignableFrom(typeof(ExceptionTermWindow)));
    }
}
