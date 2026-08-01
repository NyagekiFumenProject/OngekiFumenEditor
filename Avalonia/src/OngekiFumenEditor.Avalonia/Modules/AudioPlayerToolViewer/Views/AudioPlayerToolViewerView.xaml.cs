using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Views;

public partial class AudioPlayerToolViewerView : UserControl
{
    public AudioPlayerToolViewerView()
    {
        InitializeComponent();
        SoundControlSwitches.AddHandler(ToggleButton.IsCheckedChangedEvent, OnSoundControlSwitchChanged);
    }

    private void OnSoundControlSwitchChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is AudioPlayerToolViewerViewModel viewModel)
            viewModel.OnSoundControlSwitchChanged();
    }
}
