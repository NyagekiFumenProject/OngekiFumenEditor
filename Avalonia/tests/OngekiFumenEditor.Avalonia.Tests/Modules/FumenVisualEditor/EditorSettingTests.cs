using Avalonia.Headless.XUnit;
using OngekiFumenEditor.Avalonia.Models.Settings;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenVisualEditor;

public sealed class EditorSettingTests
{
    [AvaloniaFact]
    public void ShowXOffsetScrollBar_UpdatesOnlyItsGlobalSetting()
    {
        var globalSetting = EditorGlobalSetting.Default;
        var originalForceMagneticDock = globalSetting.ForceXGridMagneticDock;
        var originalShowXOffsetScrollBar = globalSetting.ShowXOffsetScrollBar;
        var editorSetting = new EditorSetting();

        try
        {
            globalSetting.ForceXGridMagneticDock = true;
            globalSetting.ShowXOffsetScrollBar = true;

            editorSetting.ShowXOffsetScrollBar = false;

            Assert.False(editorSetting.ShowXOffsetScrollBar);
            Assert.False(globalSetting.ShowXOffsetScrollBar);
            Assert.True(globalSetting.ForceXGridMagneticDock);

            editorSetting.ShowXOffsetScrollBar = true;

            Assert.True(editorSetting.ShowXOffsetScrollBar);
            Assert.True(globalSetting.ShowXOffsetScrollBar);
            Assert.True(globalSetting.ForceXGridMagneticDock);

            globalSetting.ShowXOffsetScrollBar = false;
            Assert.False(editorSetting.ShowXOffsetScrollBar);
        }
        finally
        {
            editorSetting.Dispose();
            globalSetting.ForceXGridMagneticDock = originalForceMagneticDock;
            globalSetting.ShowXOffsetScrollBar = originalShowXOffsetScrollBar;
        }
    }
}
