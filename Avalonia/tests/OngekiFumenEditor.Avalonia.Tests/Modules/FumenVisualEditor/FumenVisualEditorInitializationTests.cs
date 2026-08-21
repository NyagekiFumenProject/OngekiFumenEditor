using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Modules.Shell.Commands;
using Gekimini.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Assets.Languages;
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
    public void ContextBackedProperties_ForwardValuesAndNotifyBindings()
    {
        using var editor = new FumenVisualEditorViewModel();
        var context = new EditorContext
        {
            ProjectData = new EditorProjectDataModel(),
            Fumen = new OngekiFumen(),
            FilePath = "project.nyagekiProj",
            FileName = "chart.nyageki"
        };
        var changedProperties = new HashSet<string>();
        editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is { } propertyName)
                changedProperties.Add(propertyName);
        };

        editor.EditorContext = context;

        Assert.Same(context, editor.EditorContext);
        Assert.Contains(nameof(editor.EditorContext), changedProperties);

        changedProperties.Clear();
        var replacementFumen = new OngekiFumen();
        var replacementProjectData = new EditorProjectDataModel();
        context.Fumen = replacementFumen;
        context.ProjectData = replacementProjectData;
        context.FilePath = "updated/project.nyagekiProj";
        context.FileName = "updated-chart.nyageki";

        Assert.Same(replacementFumen, editor.EditorContext.Fumen);
        Assert.Same(replacementProjectData, editor.EditorContext.ProjectData);
        Assert.Equal("updated/project.nyagekiProj", editor.EditorContext.FilePath);
        Assert.Equal("updated-chart.nyageki", editor.EditorContext.FileName);
        Assert.Contains(nameof(editor.EditorContext), changedProperties);
        Assert.Contains(nameof(editor.DisplayName), changedProperties);

        // 编辑器不再代理谱面/项目数据/文件路径属性，外部统一通过 EditorContext 访问。
        Assert.Null(typeof(FumenVisualEditorViewModel).GetProperty(nameof(EditorContext.Fumen)));
        Assert.Null(typeof(FumenVisualEditorViewModel).GetProperty(nameof(EditorContext.ProjectData)));
        Assert.Null(typeof(FumenVisualEditorViewModel).GetProperty(nameof(EditorContext.FilePath)));
        Assert.Null(typeof(FumenVisualEditorViewModel).GetProperty(nameof(EditorContext.FileName)));
    }

    [AvaloniaFact]
    public void Dispose_DoesNotRecalculateTimelineAfterContextIsDetached()
    {
        var context = new EditorContext
        {
            ProjectData = new EditorProjectDataModel
            {
                AudioDuration = TimeSpan.FromSeconds(10)
            },
            Fumen = new OngekiFumen()
        };
        var editor = new FumenVisualEditorViewModel
        {
            ViewHeight = 10,
            EditorContext = context
        };
        editor.TotalDurationHeight = 1000;
        editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(editor.TotalDurationHeight))
                editor.ReverseScrollViewerVerticalOffset = editor.TotalDurationHeight;
        };

        var exception = Record.Exception(editor.Dispose);

        Assert.Null(exception);
        Assert.Null(editor.EditorContext);

        // 上下文已与编辑器解除订阅，后续上下文变更不应再触发时间轴重算。
        var totalDurationHeight = editor.TotalDurationHeight;
        context.Fumen = new OngekiFumen();
        context.ProjectData = new EditorProjectDataModel { AudioDuration = TimeSpan.FromSeconds(99) };
        Assert.Equal(totalDurationHeight, editor.TotalDurationHeight);
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
            EditorContext = new EditorContext
            {
                ProjectData = project,
                Fumen = fumen
            }
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
            EditorContext = new EditorContext
            {
                ProjectData = new EditorProjectDataModel
                {
                    AudioDuration = TimeSpan.FromSeconds(10)
                },
                Fumen = fumen
            }
        };

        editor.IsDirty = false;
        tap.IsSelected = true;
        Assert.False(editor.IsDirty);

        tap.XGrid = new XGrid(1);
        Assert.True(editor.IsDirty);
    }

    [AvaloniaFact]
    public async Task PersistedDocumentContract_DisablesSaveAsUntilDestinationFlowExists()
    {
        using var editor = new FumenVisualEditorViewModel();
        var persistedDocument = Assert.IsAssignableFrom<IPersistedDocumentViewModel>(editor);

        Assert.True(persistedDocument.IsNew);
        Assert.Contains(typeof(SaveFileCommandDefinition), editor.SupportCommandDefinitionTypes);
        Assert.Contains(typeof(SaveFileAsCommandDefinition), editor.SupportCommandDefinitionTypes);

        var saveAsCommand = new Command(new SaveFileAsCommandDefinition());
        await ((ICommandHandler)editor).Update(saveAsCommand);
        Assert.False(saveAsCommand.Enabled);
        Assert.False(await persistedDocument.SaveAs());
    }

    [AvaloniaFact]
    public void LoadedView_SubscriptionsReactToSettingsAndUndoActions()
    {
        var fumen = new OngekiFumen();
        var editor = new FumenVisualEditorViewModel
        {
            ViewHeight = 10,
            EditorContext = new EditorContext
            {
                ProjectData = new EditorProjectDataModel
                {
                    AudioDuration = TimeSpan.FromSeconds(10)
                },
                Fumen = fumen
            }
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
    public async Task LoadSuccessToast_IsQueuedUntilEditorViewLoads()
    {
        using var editor = new FumenVisualEditorViewModel();

        editor.ToastNotify(Lang.LoadProjectFileAndFumenFile);
        Assert.Null(editor.Toast);

        var view = new FumenVisualEditorView();
        editor.OnViewAfterLoaded(view);
        try
        {
            await Dispatcher.UIThread.InvokeAsync(static () => { });

            Assert.NotNull(editor.Toast);
            Assert.True(editor.Toast!.IsVisible);
            Assert.Equal(Lang.LoadProjectFileAndFumenFile, editor.Toast.Message);
        }
        finally
        {
            editor.OnViewBeforeUnload(view);
        }
    }

    [AvaloniaFact]
    public async Task LoadFailure_DoesNotShowLoadSuccessToast()
    {
        using var editor = new FumenVisualEditorViewModel();
        using var context = new EditorContext
        {
            ProjectData = new EditorProjectDataModel(),
            FileAccessContext = new EditorFileAccessContext()
        };

        Assert.False(await editor.LoadProjectAsync(context, "missing.nyagekiProj"));

        var view = new FumenVisualEditorView();
        editor.OnViewAfterLoaded(view);
        try
        {
            await Dispatcher.UIThread.InvokeAsync(static () => { });

            Assert.NotNull(editor.Toast);
            Assert.False(editor.Toast!.IsVisible);
            Assert.Empty(editor.Toast.Message);
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
            AudioDuration = TimeSpan.FromSeconds(10)
        };
        var context = new EditorContext
        {
            ProjectData = project,
            Fumen = fumen,
            FileAccessContext = new EditorFileAccessContext
            {
                ProjectFile = new LocalSimpleFile(projectPath),
                FumenFile = new LocalSimpleFile(fumenPath)
            }
        };
        var editor = new FumenVisualEditorViewModel
        {
            EditorContext = context,
            IsDirty = true
        };
        var view = new FumenVisualEditorView();
        editor.OnViewAfterLoaded(view);

        try
        {
            Assert.False(editor.IsNew);
            Assert.True(await editor.Save());
            await Dispatcher.UIThread.InvokeAsync(static () => { });
            Assert.False(editor.IsDirty);
            Assert.True(new FileInfo(projectPath).Length > 0);
            Assert.True(new FileInfo(fumenPath).Length > 0);
            Assert.NotNull(editor.Toast);
            Assert.True(editor.Toast!.IsVisible);
            Assert.Equal(Lang.SaveProjectFileAndFumenFile, editor.Toast.Message);
        }
        finally
        {
            editor.OnViewBeforeUnload(view);
            context.Dispose();
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                "OngekiFumenEditor.EditorContext.FumenVisualEditor.Tests",
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
