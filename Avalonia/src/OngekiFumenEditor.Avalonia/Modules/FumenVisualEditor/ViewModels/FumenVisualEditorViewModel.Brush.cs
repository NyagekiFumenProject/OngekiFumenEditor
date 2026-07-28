using Gekimini.Avalonia.Framework;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Behaviors.BatchMode;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

public partial class FumenVisualEditorViewModel : DocumentViewModelBase
{
    private BatchModeBehavior _batchModeBehavior = new();

    public BatchModeBehavior BatchModeBehavior
    {
        get => _batchModeBehavior;
        set => SetProperty(ref _batchModeBehavior, value);
    }
}

