using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Views.UI;

public partial class Toast : UserControl
{
    private int messageVersion;

    public enum MessageType
    {
        Error,
        Warn,
        Notify
    }

    public static readonly StyledProperty<IBrush> TextColorProperty =
        AvaloniaProperty.Register<Toast, IBrush>(nameof(TextColor), Brushes.White);

    public static readonly StyledProperty<string> MessageProperty =
        AvaloniaProperty.Register<Toast, string>(nameof(Message), string.Empty);

    private static readonly Dictionary<MessageType, IBrush> TextColors = new()
    {
        [MessageType.Error] = Brushes.Red,
        [MessageType.Warn] = Brushes.Orange,
        [MessageType.Notify] = Brushes.White
    };

    public IBrush TextColor
    {
        get => GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public string Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public Toast()
    {
        InitializeComponent();
    }

    public void ShowMessage(string message, MessageType messageType = MessageType.Notify, uint showTime = 2000)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var version = Interlocked.Increment(ref messageVersion);
        _ = InternalShowMessage(message, messageType, showTime, version);
    }

    private async Task InternalShowMessage(string message, MessageType messageType, uint showTime, int version)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Message = message;
            TextColor = TextColors.TryGetValue(messageType, out var brush) ? brush : Brushes.White;
            IsVisible = true;
            Log.LogDebug($"{messageType} {Message} ({showTime}ms)");
        });

        await Task.Delay(TimeSpan.FromMilliseconds(showTime));

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (version != Volatile.Read(ref messageVersion))
                return;

            IsVisible = false;
            Message = string.Empty;
        });
    }
}
