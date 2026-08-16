using Avalonia.Headless.XUnit;
using Gekimini.Avalonia.Modules.Shell;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenVisualEditor;

public sealed class ObjectSelectionOverloadTests
{
    [AvaloniaFact]
    public async Task TimelineOverload_TogglesSelectionAndRefreshesPropertyBrowser()
    {
        var target = new Tap { TGrid = new TGrid(1), XGrid = new XGrid(0) };
        var editor = CreateEditor(target);
        var browser = IoC.Get<IFumenObjectPropertyBrowser>();
        var shell = IoC.Get<IShell>();
        await shell.ResetLayout();

        try
        {
            editor.NotifyObjectClicked((OngekiTimelineObjectBase)target);

            Assert.True(target.IsSelected);
            Assert.Same(editor, browser.Editor);
            Assert.Same(target, Assert.Single(browser.SelectedObjects));
            Assert.Contains(editor, shell.Documents);
            Assert.Same(editor, shell.ActiveDocument);

            editor.NotifyObjectClicked((OngekiTimelineObjectBase)target);

            Assert.False(target.IsSelected);
            Assert.Empty(browser.SelectedObjects);
        }
        finally
        {
            await CleanupAsync(editor, browser, shell);
        }
    }

    [AvaloniaFact]
    public async Task TimelineAndObjectBaseOverloads_UseSameMutualExclusionPath()
    {
        var first = new Tap { TGrid = new TGrid(1), XGrid = new XGrid(-1) };
        var second = new Tap { TGrid = new TGrid(2), XGrid = new XGrid(1) };
        var editor = CreateEditor(first, second);
        var browser = IoC.Get<IFumenObjectPropertyBrowser>();
        var shell = IoC.Get<IShell>();
        await shell.ResetLayout();

        try
        {
            editor.NotifyObjectClicked((OngekiObjectBase)first);
            editor.NotifyObjectClicked((OngekiTimelineObjectBase)second);

            Assert.False(first.IsSelected);
            Assert.True(second.IsSelected);
            Assert.Same(second, Assert.Single(editor.SelectObjects));
            Assert.Same(second, Assert.Single(browser.SelectedObjects));
        }
        finally
        {
            await CleanupAsync(editor, browser, shell);
        }
    }

    [AvaloniaFact]
    public async Task NavigateBehavior_ScrollsSelectsAndActivatesTargetEditor()
    {
        var target = new Tap { TGrid = new TGrid(4), XGrid = new XGrid(0) };
        var editor = CreateEditor(target);
        var browser = IoC.Get<IFumenObjectPropertyBrowser>();
        var shell = IoC.Get<IShell>();
        var expectedTime = TGridCalculator.ConvertTGridToAudioTime(target.TGrid, editor);
        await shell.ResetLayout();

        try
        {
            new NavigateToObjectBehavior(target).Navigate(editor);

            Assert.Equal(expectedTime, editor.CurrentPlayTime);
            Assert.True(target.IsSelected);
            Assert.Same(target, Assert.Single(browser.SelectedObjects));
            Assert.Same(editor, shell.ActiveDocument);
        }
        finally
        {
            await CleanupAsync(editor, browser, shell);
        }
    }

    private static FumenVisualEditorViewModel CreateEditor(params OngekiObjectBase[] objects)
    {
        var fumen = new OngekiFumen();
        foreach (var obj in objects)
            fumen.AddObject(obj);

        var project = new EditorProjectDataModel
        {
            AudioDuration = TimeSpan.FromSeconds(30)
        };
        return new FumenVisualEditorViewModel
        {
            EditorContext = new EditorContext
            {
                ProjectData = project,
                Fumen = fumen
            },
            IsDirty = false
        };
    }

    private static async Task CleanupAsync(
        FumenVisualEditorViewModel editor,
        IFumenObjectPropertyBrowser browser,
        IShell shell)
    {
        browser.RefreshSelected((FumenVisualEditorViewModel)null!);
        if (shell.Documents.Contains(editor))
            await shell.CloseDocumentAsync(editor);
        editor.EditorContext?.Dispose();
        editor.Setting.Dispose();
    }
}
