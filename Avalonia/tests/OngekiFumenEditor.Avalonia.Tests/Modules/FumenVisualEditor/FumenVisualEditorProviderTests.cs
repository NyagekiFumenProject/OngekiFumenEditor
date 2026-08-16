using Avalonia.Headless.XUnit;
using Gekimini.Avalonia.Framework.Documents;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenVisualEditor;

public sealed class FumenVisualEditorProviderTests
{
    [AvaloniaFact]
    public async Task TryNew_FumenVisualEditor_DelegatesToViewModelNew()
    {
        var provider = new FumenVisualEditorProvider();
        var editor = new TrackingFumenVisualEditorViewModel
        {
            NewResult = true
        };

        var result = await provider.TryNew(editor);

        Assert.True(result);
        Assert.Equal(1, editor.NewCallCount);
    }

    [AvaloniaFact]
    public async Task TryOpen_ContextWithoutAudio_DoesNotAttachOrDisposeContext()
    {
        var provider = new FumenVisualEditorProvider();
        var editor = new FumenVisualEditorViewModel();
        using var context = new EditorContext
        {
            ProjectData = new EditorProjectDataModel(),
            FileAccessContext = new EditorFileAccessContext()
        };

        var result = await provider.TryOpen(editor, context);

        Assert.False(result);
        Assert.Null(editor.EditorContext);
        Assert.NotNull(context.FileAccessContext);
    }

    [AvaloniaFact]
    public async Task TryOpen_ContextForDifferentDocument_ReturnsFalseAndRetainsOwnership()
    {
        var provider = new FumenVisualEditorProvider();
        var document = new OtherDocumentViewModel();
        using var context = new EditorContext
        {
            ProjectData = new EditorProjectDataModel(),
            FileAccessContext = new EditorFileAccessContext()
        };

        var result = await provider.TryOpen(document, context);

        Assert.False(result);
        Assert.NotNull(context.FileAccessContext);
    }

    private sealed class TrackingFumenVisualEditorViewModel : FumenVisualEditorViewModel
    {
        public bool NewResult { get; init; }

        public int NewCallCount { get; private set; }

        public override Task<bool> New()
        {
            NewCallCount++;
            return Task.FromResult(NewResult);
        }
    }

    private sealed class OtherDocumentViewModel : DocumentViewModelBase
    {
    }
}
