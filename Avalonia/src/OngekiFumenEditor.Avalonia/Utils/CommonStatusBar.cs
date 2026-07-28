using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Utils;

[RegisterSingleton<CommonStatusBar>]
public class CommonStatusBar
{
    public sealed class StatusBarItemViewModel
    {
        public string Message { get; set; } = string.Empty;
    }

    public StatusBarItemViewModel MainContentViewModel { get; } = new();
    public StatusBarItemViewModel SubLeftContentViewModel { get; } = new();
    public StatusBarItemViewModel SubRightMainContentViewModel { get; } = new();
}
