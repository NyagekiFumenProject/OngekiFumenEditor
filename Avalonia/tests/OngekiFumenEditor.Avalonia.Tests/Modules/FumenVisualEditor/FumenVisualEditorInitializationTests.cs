using Avalonia.Headless.XUnit;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Modules.Shell.Commands;
using Gekimini.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.LocalFileSystem;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenVisualEditor;

public sealed class FumenVisualEditorInitializationTests
{
    [AvaloniaFact]
    public void Constructor_InitializesInactiveSelectionArea()
    {
        var editor = new FumenVisualEditorViewModel();

        Assert.NotNull(editor.SelectionArea);
        Assert.Equal(SelectionAreaKind.Select, editor.SelectionArea.SelectionAreaKind);
        Assert.False(editor.SelectionArea.IsActive);
        Assert.False(editor.IsRangeSelecting);
    }

    [AvaloniaFact]
    public void ProjectAndTimingChanges_RecalculateTimelineExtentAndMarkDocumentDirty()
    {
        var fumen = new OngekiFumen();
        var project = new EditorProjectDataModel
        {
            AudioDuration = TimeSpan.FromSeconds(10),
            Fumen = fumen
        };
        var editor = new FumenVisualEditorViewModel
        {
            ViewHeight = 10,
            EditorProjectData = project,
            Fumen = fumen
        };

        var initialHeight = editor.TotalDurationHeight;
        Assert.True(initialHeight > editor.ViewHeight);

        editor.IsDirty = false;
        project.AudioDuration = TimeSpan.FromSeconds(20);

        Assert.True(editor.IsDirty);
        Assert.True(editor.TotalDurationHeight > initialHeight);

        var durationHeight = editor.TotalDurationHeight;
        editor.TotalDurationHeight = 123;
        fumen.BpmList.FirstBpm = 120;

        Assert.Equal(durationHeight, editor.TotalDurationHeight);
    }

    [AvaloniaFact]
    public void FumenObjectChanges_IgnoreSelectionButMarkContentChangesDirty()
    {
        var fumen = new OngekiFumen();
        var tap = new Tap();
        fumen.AddObject(tap);
        var editor = new FumenVisualEditorViewModel
        {
            EditorProjectData = new EditorProjectDataModel
            {
                AudioDuration = TimeSpan.FromSeconds(10),
                Fumen = fumen
            },
            Fumen = fumen
        };

        editor.IsDirty = false;
        tap.IsSelected = true;
        Assert.False(editor.IsDirty);

        tap.XGrid = new XGrid(1);
        Assert.True(editor.IsDirty);
    }

    [AvaloniaFact]
    public async Task PersistedDocumentContract_ExposesSaveButNotSaveAs()
    {
        var editor = new FumenVisualEditorViewModel();
        var persistedDocument = Assert.IsAssignableFrom<IPersistedDocumentViewModel>(editor);

        Assert.True(persistedDocument.IsNew);
        Assert.Contains(typeof(SaveFileCommandDefinition), editor.SupportCommandDefinitionTypes);
        Assert.DoesNotContain(typeof(SaveFileAsCommandDefinition), editor.SupportCommandDefinitionTypes);
        Assert.False(await persistedDocument.SaveAs());
    }

    [AvaloniaFact]
    public void LoadedView_SubscriptionsReactToSettingsAndUndoActions()
    {
        var fumen = new OngekiFumen();
        var editor = new FumenVisualEditorViewModel
        {
            ViewHeight = 10,
            EditorProjectData = new EditorProjectDataModel
            {
                AudioDuration = TimeSpan.FromSeconds(10),
                Fumen = fumen
            },
            Fumen = fumen
        };
        var view = new FumenVisualEditorView();

        editor.OnViewAfterLoaded(view);
        try
        {
            var expectedHeight = editor.TotalDurationHeight;
            editor.TotalDurationHeight = 123;
            editor.Setting.VerticalDisplayScale = editor.Setting.VerticalDisplayScale;
            Assert.Equal(expectedHeight, editor.TotalDurationHeight);

            editor.IsDirty = false;
            editor.UndoRedoManager.ExecuteAction(new LambdaUndoAction(
                LocalizedString.CreateFromRawText("test action"),
                static () => { },
                static () => { }));
            Assert.True(editor.IsDirty);
        }
        finally
        {
            editor.OnViewBeforeUnload(view);
        }
    }

    [AvaloniaFact]
    public async Task Save_SuccessfullyWritesProjectAndFumenAndClearsDirty()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var projectPath = temporaryDirectory.File("project.nyagekiProj");
        var fumenPath = temporaryDirectory.File("chart.nyageki");
        await File.WriteAllBytesAsync(projectPath, []);
        await File.WriteAllBytesAsync(fumenPath, []);

        var fumen = new OngekiFumen();
        var project = new EditorProjectDataModel
        {
            AudioDuration = TimeSpan.FromSeconds(10),
            FumenFilePath = "chart.nyageki",
            ProjectFileLocator = "project.nyagekiProj",
            Fumen = fumen,
            ProjectFile = new LocalSimpleFile(projectPath),
            FumenFile = new LocalSimpleFile(fumenPath)
        };
        var editor = new FumenVisualEditorViewModel
        {
            EditorProjectData = project,
            Fumen = fumen,
            IsDirty = true
        };

        try
        {
            Assert.False(editor.IsNew);
            Assert.True(await editor.Save());
            Assert.False(editor.IsDirty);
            Assert.True(new FileInfo(projectPath).Length > 0);
            Assert.True(new FileInfo(fumenPath).Length > 0);
        }
        finally
        {
            project.DisposeRuntimeFiles();
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                "OngekiFumenEditor.FumenVisualEditor.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public string File(string relativePath) => Path.Combine(RootPath, relativePath);

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
    }
}
