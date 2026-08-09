#nullable enable
using Gekimini.Avalonia.Framework;
using Avalonia.Xaml.Interactivity;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Behaviors.BatchMode;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Views;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

public partial class FumenVisualEditorViewModel : DocumentViewModelBase
{
    private BatchModeBehavior _batchModeBehavior = new();
    private BatchModeBehavior? attachedBatchModeBehavior;
    private FumenVisualEditorView? attachedBatchModeBehaviorView;

    public BatchModeBehavior BatchModeBehavior
    {
        get => _batchModeBehavior;
        set
        {
            if (ReferenceEquals(_batchModeBehavior, value))
                return;

            DetachBatchModeBehavior();
            if (SetProperty(ref _batchModeBehavior, value))
                UpdateBatchModeBehaviorAttachment();
        }
    }

    partial void OnIsBatchModeChanged(bool value)
    {
        UpdateBatchModeBehaviorAttachment();
        OnPropertyChanged(nameof(EnableDragging));
    }

    private void UpdateBatchModeBehaviorAttachment()
    {
        if (!IsBatchMode || View is null)
        {
            DetachBatchModeBehavior();
            return;
        }

        if (ReferenceEquals(attachedBatchModeBehavior, BatchModeBehavior) &&
            ReferenceEquals(attachedBatchModeBehaviorView, View))
        {
            return;
        }

        DetachBatchModeBehavior();
        var behaviors = Interaction.GetBehaviors(View);
        if (!behaviors.Contains(BatchModeBehavior))
            behaviors.Add(BatchModeBehavior);
        attachedBatchModeBehavior = BatchModeBehavior;
        attachedBatchModeBehaviorView = View;
    }

    private void DetachBatchModeBehavior()
    {
        if (attachedBatchModeBehavior is null || attachedBatchModeBehaviorView is null)
            return;

        Interaction.GetBehaviors(attachedBatchModeBehaviorView).Remove(attachedBatchModeBehavior);
        attachedBatchModeBehavior = null;
        attachedBatchModeBehaviorView = null;
    }
}

