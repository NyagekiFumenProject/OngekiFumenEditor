using Gemini.Framework;
using OngekiFumenEditor.Modules.FumenVisualEditor.Behaviors.BatchMode;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;

public partial class FumenVisualEditorViewModel : PersistedDocument
{
    private BatchModeBehavior _batchModeBehavior = new();

    public BatchModeBehavior BatchModeBehavior
    {
        get => _batchModeBehavior;
        set => Set(ref _batchModeBehavior, value);
    }
}