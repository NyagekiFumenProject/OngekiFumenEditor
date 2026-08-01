using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;

namespace OngekiFumenEditor.Avalonia.Modules.FumenTimeSignatureListViewer.ViewModels;

public sealed class DisplayTimeSignatureItem : ObservableObject
{
    public TimeSpan StartAudioTime
    {
        get => field;
        set => SetProperty(ref field, value);
    }

    public TGrid StartTGrid
    {
        get => field;
        set => SetProperty(ref field, value);
    }

    public MeterChange Meter
    {
        get => field;
        set => SetProperty(ref field, value);
    }

    public BPMChange BPMChange
    {
        get => field;
        set => SetProperty(ref field, value);
    }
}
