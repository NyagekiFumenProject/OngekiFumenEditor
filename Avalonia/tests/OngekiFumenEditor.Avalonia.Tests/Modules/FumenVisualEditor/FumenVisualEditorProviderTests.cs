using Avalonia.Headless.XUnit;
using Gekimini.Avalonia.Framework.Documents;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Setup;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenVisualEditor;

public sealed class FumenVisualEditorProviderTests
{
    [AvaloniaFact]
    public async Task TryNew_WhenCreationIsDisabled_ReturnsFalse()
    {
        var provider = new TestProvider(canCreateNew: false);
        var editor = new TrackingFumenVisualEditorViewModel
        {
            NewResult = true
        };

        var result = await provider.TryNew(editor);

        Assert.False(result);
        Assert.Equal(0, editor.NewCallCount);
    }

    [AvaloniaFact]
    public async Task TryOpen_ContextWithoutAudio_DoesNotAttachOrDisposeContext()
    {
        var provider = new TestProvider(canCreateNew: false);
        var editor = new FumenVisualEditorViewModel(Microsoft.Extensions.Logging.Abstractions.NullLogger<FumenVisualEditorViewModel>.Instance);
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
        var provider = new TestProvider(canCreateNew: false);
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
        public TrackingFumenVisualEditorViewModel()
            : base(Microsoft.Extensions.Logging.Abstractions.NullLogger<FumenVisualEditorViewModel>.Instance)
        {
        }
        public bool NewResult { get; init; }

        public int NewCallCount { get; private set; }

        public override Task<bool> New()
        {
            NewCallCount++;
            return Task.FromResult(NewResult);
        }
    }

    private sealed class TestProvider : FumenVisualEditorProviderBase
    {
        private readonly bool canCreateNew;

        public TestProvider(bool canCreateNew)
        {
            this.canCreateNew = canCreateNew;
        }

        public override bool CanCreateNew => canCreateNew;

        protected override IEditorProjectSetupFilePicker CreateSetupFilePicker() =>
            throw new NotSupportedException();

        protected override Task<EditorFileAccessContext> RestoreContextAsync(
            EditorFileAccessContextSnapshot snapshot,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class OtherDocumentViewModel : DocumentViewModelBase
    {
    }
}
