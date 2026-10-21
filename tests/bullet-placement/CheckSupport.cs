using System.Numerics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Caliburn.Micro;
using Gemini.Framework.Services;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;

internal static class CheckSupport
{
    public static FumenVisualEditorViewModel CreateEditor()
    {
        var editor = new FumenVisualEditorViewModel { EditorProjectData = new EditorProjectDataModel() };
        editor.Fumen.BpmList.FirstBpm = 120;
        editor.Setting.DisableTGridMagneticDock = true;
        editor.Setting.DisableXGridMagneticDock = true;
        editor.Setting.ForceMagneticDock = false;
        editor.Setting.ForceXGridMagneticDock = false;
        editor.Setting.ForceXGridIntegerUnit = false;
        editor.Setting.EnableXOffset = false;
        editor.Setting.XOffset = 0;
        editor.Setting.XGridDisplayMaxUnit = 8;
        editor.Setting.XGridUnitSpace = 1;
        editor.Setting.VerticalDisplayScale = 1;
        editor.Setting.JudgeLineOffsetY = 0;
        editor.Setting.BeatSplit = 4;
        SetViewport(editor);
        IoC.Get<IShell>().ActiveLayoutItem = editor;
        return editor;
    }

    public static void SetViewport(FumenVisualEditorViewModel editor, float bottom = 0, float width = 800)
    {
        editor.ViewWidth = width;
        editor.ViewHeight = 600;
        editor.RectInDesignMode = new VisibleRect(new Vector2(width, bottom), new Vector2(0, bottom + 600));
        editor.TotalDurationHeight = 100000;
    }

    public static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    public static void Arrange(FrameworkElement view)
    {
        Pump();
        view.Measure(new Size(1200, 320));
        view.Arrange(new Rect(0, 0, 1200, 320));
        view.UpdateLayout();
        Pump();
        view.UpdateLayout();
    }

    public static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}
