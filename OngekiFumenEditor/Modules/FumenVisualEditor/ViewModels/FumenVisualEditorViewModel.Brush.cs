using Gemini.Framework;
using OngekiFumenEditor.Modules.FumenVisualEditor.Behaviors.BrushMode;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;

public partial class FumenVisualEditorViewModel : PersistedDocument
{
    private BrushModeBehavior _brushModeBehavior = new();

    public BrushModeBehavior BrushModeBehavior
    {
        get => _brushModeBehavior;
        set => Set(ref _brushModeBehavior, value);
    }
}