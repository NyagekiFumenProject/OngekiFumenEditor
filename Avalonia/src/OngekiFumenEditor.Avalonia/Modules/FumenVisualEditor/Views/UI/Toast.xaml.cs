using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Views.UI;

public partial class Toast : UserControl
{
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(750);
    private readonly object messageGate = new();
    private CancellationTokenSource messageCancellation;
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

        int version;
        CancellationTokenSource cancellation;
        lock (messageGate)
        {
            version = Interlocked.Increment(ref messageVersion);
            cancellation = new CancellationTokenSource();
            var previousCancellation = messageCancellation;
            messageCancellation = cancellation;
            previousCancellation?.Cancel();
        }

        _ = InternalShowMessage(message, messageType, showTime, version, cancellation);
    }

    private async Task InternalShowMessage(
        string message,
        MessageType messageType,
        uint showTime,
        int version,
        CancellationTokenSource cancellation)
    {
        var cancellationToken = cancellation.Token;
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!IsCurrentMessage(version, cancellationToken))
                    return;

                Message = message;
                TextColor = TextColors.TryGetValue(messageType, out var brush) ? brush : Brushes.White;
                Opacity = 0;
                IsVisible = true;
                Log.LogDebug($"{messageType} {Message} ({showTime}ms)");
            });

            if (!IsCurrentMessage(version, cancellationToken))
                return;

            await RunAnimationAsync(showTime, version, cancellationToken);
            if (!IsCurrentMessage(version, cancellationToken))
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!IsCurrentMessage(version, cancellationToken))
                    return;

                IsVisible = false;
                Message = string.Empty;
                Opacity = 0;
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer message owns the Toast now.
        }
        finally
        {
            lock (messageGate)
            {
                if (ReferenceEquals(messageCancellation, cancellation))
                    messageCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private Task RunAnimationAsync(uint showTime, int version, CancellationToken cancellationToken)
    {
        return Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (!IsCurrentMessage(version, cancellationToken))
                return;

            // Keep the original Toast timing: fade in, hold until showTime, then fade out.
            var displayMilliseconds = Math.Max(1d, showTime);
            var totalMilliseconds = displayMilliseconds + FadeDuration.TotalMilliseconds;
            var fadeInMilliseconds = displayMilliseconds >= FadeDuration.TotalMilliseconds
                ? FadeDuration.TotalMilliseconds
                : displayMilliseconds / 2d;
            var fadeInCue = fadeInMilliseconds / totalMilliseconds;
            var fadeOutCue = displayMilliseconds / totalMilliseconds;
            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(totalMilliseconds),
                Easing = new LinearEasing(),
                FillMode = FillMode.Forward,
                Children =
                {
                    CreateOpacityKeyFrame(0, 0),
                    CreateOpacityKeyFrame(fadeInCue, 1),
                    CreateOpacityKeyFrame(fadeOutCue, 1),
                    CreateOpacityKeyFrame(1, 0)
                }
            };

            await animation.RunAsync(this, cancellationToken);

            if (IsCurrentMessage(version, cancellationToken))
                Opacity = 0;
        });
    }

    private static KeyFrame CreateOpacityKeyFrame(double cue, double opacity)
    {
        return new KeyFrame
        {
            Cue = new Cue(cue),
            Setters =
            {
                new Setter(Visual.OpacityProperty, opacity)
            }
        };
    }

    private bool IsCurrentMessage(int version, CancellationToken cancellationToken)
    {
        return !cancellationToken.IsCancellationRequested && version == Volatile.Read(ref messageVersion);
    }
}
