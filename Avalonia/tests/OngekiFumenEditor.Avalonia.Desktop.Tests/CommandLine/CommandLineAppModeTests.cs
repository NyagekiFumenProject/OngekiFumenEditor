using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine;

public sealed class CommandLineAppModeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void ShouldCreateMainView_FollowsGuiMode(bool isGuiMode, bool expected)
    {
        var app = new InspectableDesktopApp(isGuiMode);

        Assert.Equal(expected, app.CreatesMainView);
    }

    private sealed class InspectableDesktopApp(bool isGuiMode)
        : OngekiFumenEditorDesktopApp(isGuiMode)
    {
        public bool CreatesMainView => ShouldCreateMainView;
    }
}
