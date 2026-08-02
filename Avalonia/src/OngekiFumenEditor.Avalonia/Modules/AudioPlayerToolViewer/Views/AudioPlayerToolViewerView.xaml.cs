using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Gekimini.Avalonia.Views;
using OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Views;

public partial class AudioPlayerToolViewerView : ViewBase
{
    private CancellationTokenSource waveformHostCancellationSource;
    private AudioPlayerToolViewerViewModel attachedWaveformViewModel;
    private bool isWaveformHostLoaded;

    public AudioPlayerToolViewerView()
    {
        InitializeComponent();
        SoundControlSwitches.AddHandler(ToggleButton.IsCheckedChangedEvent, OnSoundControlSwitchChanged);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnSoundControlSwitchChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is AudioPlayerToolViewerViewModel viewModel)
            viewModel.OnSoundControlSwitchChanged();
    }

    private void OnWaveformHostLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ContentControl host)
            return;

        isWaveformHostLoaded = true;
        BeginAttachWaveformHost(host);
    }

    private void OnWaveformHostUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ContentControl host)
            return;

        isWaveformHostLoaded = false;
        DetachWaveformHost(host);
    }

    private void OnDataContextChanged(object sender, EventArgs e)
    {
        if (isWaveformHostLoaded)
            BeginAttachWaveformHost(renderControlHost);
    }

    private void BeginAttachWaveformHost(ContentControl host)
    {
        DetachWaveformHost(host);
        if (DataContext is not AudioPlayerToolViewerViewModel viewModel)
            return;

        var cancellationSource = new CancellationTokenSource();
        waveformHostCancellationSource = cancellationSource;
        attachedWaveformViewModel = viewModel;
        _ = AttachWaveformHostAsync(viewModel, host, cancellationSource.Token);
    }

    private static async Task AttachWaveformHostAsync(
        AudioPlayerToolViewerViewModel viewModel,
        ContentControl host,
        CancellationToken cancellationToken)
    {
        try
        {
            await viewModel.AttachWaveformHostAsync(host, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Log.LogError("Failed to attach the waveform render control.", e);
        }
    }

    private void DetachWaveformHost(ContentControl host)
    {
        var cancellationSource = Interlocked.Exchange(ref waveformHostCancellationSource, null);
        if (cancellationSource is not null)
        {
            cancellationSource.Cancel();
            cancellationSource.Dispose();
        }

        var viewModel = attachedWaveformViewModel;
        attachedWaveformViewModel = null;
        viewModel?.DetachWaveformHost(host);
    }
}
