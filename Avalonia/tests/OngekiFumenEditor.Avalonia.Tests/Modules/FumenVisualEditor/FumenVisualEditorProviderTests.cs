using Avalonia.Headless.XUnit;
using Gekimini.Avalonia.Framework.RecentFiles;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
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
        Assert.Equal(0, editor.LoadCallCount);
        Assert.Equal(0, editor.RecentLoadCallCount);
    }

    [AvaloniaFact]
    public async Task TryOpen_FumenVisualEditor_DelegatesToViewModelLoad()
    {
        var provider = new FumenVisualEditorProvider();
        var editor = new TrackingFumenVisualEditorViewModel
        {
            LoadResult = true
        };

        var result = await provider.TryOpen(editor);

        Assert.True(result);
        Assert.Equal(0, editor.NewCallCount);
        Assert.Equal(1, editor.LoadCallCount);
        Assert.Equal(0, editor.RecentLoadCallCount);
    }

    [AvaloniaFact]
    public async Task TryOpen_RecentRecord_DelegatesToViewModelRecentLoadWithSameRecord()
    {
        var provider = new FumenVisualEditorProvider();
        var editor = new TrackingFumenVisualEditorViewModel
        {
            RecentLoadResult = true
        };
        var recordInfo = new RecentRecordInfo(
            "FumenVisualEditorProject",
            "project.nyagekiProj",
            "ProjectFolder/project.nyagekiProj",
            RecordId: Guid.NewGuid());

        var result = await provider.TryOpen(editor, recordInfo);

        Assert.True(result);
        Assert.Same(recordInfo, editor.LastRecentRecord);
        Assert.Equal(0, editor.NewCallCount);
        Assert.Equal(0, editor.LoadCallCount);
        Assert.Equal(1, editor.RecentLoadCallCount);
    }

    private sealed class TrackingFumenVisualEditorViewModel : FumenVisualEditorViewModel
    {
        public bool NewResult { get; init; }
        public bool LoadResult { get; init; }
        public bool RecentLoadResult { get; init; }

        public int NewCallCount { get; private set; }
        public int LoadCallCount { get; private set; }
        public int RecentLoadCallCount { get; private set; }
        public RecentRecordInfo? LastRecentRecord { get; private set; }

        public override Task<bool> New()
        {
            NewCallCount++;
            return Task.FromResult(NewResult);
        }

        public override Task<bool> Load()
        {
            LoadCallCount++;
            return Task.FromResult(LoadResult);
        }

        public override Task<bool> Load(RecentRecordInfo recordInfo)
        {
            RecentLoadCallCount++;
            LastRecentRecord = recordInfo;
            return Task.FromResult(RecentLoadResult);
        }
    }
}
