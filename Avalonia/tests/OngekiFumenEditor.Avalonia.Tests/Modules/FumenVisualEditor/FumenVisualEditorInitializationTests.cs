using Avalonia.Headless.XUnit;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Modules.Shell.Commands;
using Gekimini.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Models.Settings;
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
            AudioDuration = TimeSpan.FromSeconds(10)
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
                AudioDuration = TimeSpan.FromSeconds(10)
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
                AudioDuration = TimeSpan.FromSeconds(10)
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
    public void LoadedView_AppliesUndoHistoryLimitAndRuntimeChanges()
    {
        var globalSetting = EditorGlobalSetting.Default;
        var originalEnabled = globalSetting.IsEnableUndoActionSavingLimit;
        var originalLimit = globalSetting.UndoActionSavingLimit;
        var editor = new FumenVisualEditorViewModel();
        var view = new FumenVisualEditorView();
        var isLoaded = false;

        try
        {
            globalSetting.IsEnableUndoActionSavingLimit = false;
            globalSetting.UndoActionSavingLimit = 3;

            editor.OnViewAfterLoaded(view);
            isLoaded = true;
            Assert.Null(editor.UndoRedoManager.UndoCountLimit);

            globalSetting.IsEnableUndoActionSavingLimit = true;
            Assert.Equal(3, editor.UndoRedoManager.UndoCountLimit);

            var value = 0;
            for (var i = 0; i < 5; i++)
                editor.UndoRedoManager.ExecuteAction(CreateIncrementAction(() => value++, () => value--));

            Assert.Equal(5, value);
            Assert.Equal(3, editor.UndoRedoManager.ActionStack.Count);
            Assert.Equal(3, editor.UndoRedoManager.UndoActionCount);

            editor.UndoRedoManager.Undo(3);
            Assert.Equal(2, value);
            editor.UndoRedoManager.Redo(3);
            Assert.Equal(5, value);

            globalSetting.UndoActionSavingLimit = 2;
            Assert.Equal(2, editor.UndoRedoManager.ActionStack.Count);
            Assert.Equal(2, editor.UndoRedoManager.UndoActionCount);

            editor.UndoRedoManager.Undo(2);
            Assert.Equal(3, value);
            editor.UndoRedoManager.Redo(2);
            Assert.Equal(5, value);

            editor.UndoRedoManager.Clear();
            value = 0;
            editor.UndoRedoManager.BeginCombineAction();
            editor.UndoRedoManager.ExecuteAction(CreateIncrementAction(() => value++, () => value--));
            editor.UndoRedoManager.ExecuteAction(CreateIncrementAction(() => value++, () => value--));
            editor.UndoRedoManager.ExecuteAction(CreateIncrementAction(() => value++, () => value--));
            var combinedAction = editor.UndoRedoManager.EndCombineAction(
                LocalizedString.CreateFromRawText("combined test action"));
            editor.UndoRedoManager.ExecuteAction(combinedAction);

            Assert.Equal(3, value);
            Assert.Single(editor.UndoRedoManager.ActionStack);
            editor.UndoRedoManager.Undo(1);
            Assert.Equal(0, value);
            editor.UndoRedoManager.Redo(1);
            Assert.Equal(3, value);

            globalSetting.IsEnableUndoActionSavingLimit = false;
            Assert.Null(editor.UndoRedoManager.UndoCountLimit);

            editor.OnViewBeforeUnload(view);
            isLoaded = false;
            globalSetting.IsEnableUndoActionSavingLimit = true;
            globalSetting.UndoActionSavingLimit = 7;
            Assert.Null(editor.UndoRedoManager.UndoCountLimit);

            editor.OnViewAfterLoaded(view);
            isLoaded = true;
            Assert.Equal(7, editor.UndoRedoManager.UndoCountLimit);

            globalSetting.IsEnableUndoActionSavingLimit = false;
            editor.UndoRedoManager.Clear();
            value = 0;
            for (var i = 0; i < 5; i++)
                editor.UndoRedoManager.ExecuteAction(CreateIncrementAction(() => value++, () => value--));
            editor.UndoRedoManager.Undo(4);
            Assert.Equal(1, value);

            globalSetting.UndoActionSavingLimit = 2;
            globalSetting.IsEnableUndoActionSavingLimit = true;

            Assert.Equal(4, editor.UndoRedoManager.UndoCountLimit);
            Assert.Equal(4, editor.UndoRedoManager.ActionStack.Count);
            Assert.Equal(0, editor.UndoRedoManager.UndoActionCount);
            Assert.Equal(4, editor.UndoRedoManager.RedoActionCount);

            editor.UndoRedoManager.Redo(4);
            Assert.Equal(5, value);
            Assert.Equal(2, editor.UndoRedoManager.UndoCountLimit);
            Assert.Equal(2, editor.UndoRedoManager.ActionStack.Count);
            Assert.Equal(2, editor.UndoRedoManager.UndoActionCount);
            editor.UndoRedoManager.Undo(2);
            Assert.Equal(3, value);
        }
        finally
        {
            if (isLoaded)
                editor.OnViewBeforeUnload(view);
            editor.Setting.Dispose();
            globalSetting.IsEnableUndoActionSavingLimit = originalEnabled;
            globalSetting.UndoActionSavingLimit = originalLimit;
        }
    }

    [AvaloniaFact]
    public void LoadedViews_ClampInvalidLimitAndKeepDocumentHistoriesIndependent()
    {
        var globalSetting = EditorGlobalSetting.Default;
        var originalEnabled = globalSetting.IsEnableUndoActionSavingLimit;
        var originalLimit = globalSetting.UndoActionSavingLimit;
        var firstEditor = new FumenVisualEditorViewModel();
        var secondEditor = new FumenVisualEditorViewModel();
        var firstView = new FumenVisualEditorView();
        var secondView = new FumenVisualEditorView();

        try
        {
            globalSetting.UndoActionSavingLimit = -1;
            globalSetting.IsEnableUndoActionSavingLimit = true;
            firstEditor.OnViewAfterLoaded(firstView);
            secondEditor.OnViewAfterLoaded(secondView);

            Assert.Equal(0, firstEditor.UndoRedoManager.UndoCountLimit);
            Assert.Equal(0, secondEditor.UndoRedoManager.UndoCountLimit);

            var firstValue = 0;
            firstEditor.UndoRedoManager.ExecuteAction(
                CreateIncrementAction(() => firstValue++, () => firstValue--));
            Assert.Equal(1, firstValue);
            Assert.Empty(firstEditor.UndoRedoManager.ActionStack);
            Assert.False(firstEditor.UndoRedoManager.CanUndo);

            globalSetting.UndoActionSavingLimit = 2;
            for (var i = 0; i < 3; i++)
                firstEditor.UndoRedoManager.ExecuteAction(
                    CreateIncrementAction(() => firstValue++, () => firstValue--));

            var secondValue = 0;
            secondEditor.UndoRedoManager.ExecuteAction(
                CreateIncrementAction(() => secondValue++, () => secondValue--));

            Assert.Equal(2, firstEditor.UndoRedoManager.ActionStack.Count);
            Assert.Single(secondEditor.UndoRedoManager.ActionStack);

            firstEditor.UndoRedoManager.Undo(2);
            Assert.Equal(2, firstValue);
            Assert.Equal(1, secondValue);
            Assert.True(secondEditor.UndoRedoManager.CanUndo);
        }
        finally
        {
            firstEditor.OnViewBeforeUnload(firstView);
            secondEditor.OnViewBeforeUnload(secondView);
            firstEditor.Setting.Dispose();
            secondEditor.Setting.Dispose();
            globalSetting.IsEnableUndoActionSavingLimit = originalEnabled;
            globalSetting.UndoActionSavingLimit = originalLimit;
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
            FumenFilePath = "chart.nyageki"
        };
        var context = new EditorContext
        {
            ProjectData = project,
            Fumen = fumen,
            ProjectFileLocator = "project.nyagekiProj",
            FileAccessContext = new EditorFileAccessContext
            {
                ProjectFile = new LocalSimpleFile(projectPath),
                FumenFile = new LocalSimpleFile(fumenPath)
            }
        };
        var editor = new FumenVisualEditorViewModel
        {
            EditorProjectData = project,
            EditorContext = context,
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
            context.Dispose();
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

    private static LambdaUndoAction CreateIncrementAction(Action execute, Action undo)
    {
        return new LambdaUndoAction(
            LocalizedString.CreateFromRawText("test action"),
            execute,
            undo);
    }
}
