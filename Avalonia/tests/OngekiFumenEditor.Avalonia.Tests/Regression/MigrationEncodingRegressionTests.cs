using System.Text;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Avalonia.Utils;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Regression;

public sealed class MigrationEncodingRegressionTests
{
    private static readonly string[] SourceFiles =
    [
        "src/OngekiFumenEditor.Avalonia/Base/OngekiObjectBase.cs",
        "src/OngekiFumenEditor.Avalonia/Base/OngekiObjects/ConnectableObject/ConnectableChildObjectBase.cs",
        "src/OngekiFumenEditor.Avalonia/Base/OngekiObjects/Projectiles/Bell.cs",
        "src/OngekiFumenEditor.Avalonia/Utils/Ogkr/StandardizeFormat.cs",
        "src/OngekiFumenEditor.Avalonia/UI/Controls/ObjectInspector/UIGenerator/PropertySetAction.cs",
        "src/OngekiFumenEditor.Avalonia/Modules/FumenCheckerListViewer/Base/DefaultRulesImpl/CommonObjectTimelineNotAlignedCheckRule.cs",
        "src/OngekiFumenEditor.Avalonia/Modules/FumenCheckerListViewer/Base/DefaultRulesImpl/MissingHoldEndObjectCheckRule.cs",
        "src/OngekiFumenEditor.Avalonia/Modules/FumenCheckerListViewer/Base/DefaultRulesImpl/WallConflictCheckRule.cs",
        "src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Models/EditorSetting.cs",
        "src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.UserInteractionActions.cs",
        "src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/RenderControls/Backends/OpenGL/SkiaRenderControl_OpenGL.cs",
        "src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/RenderControls/Backends/DirectX/SkiaRenderControl_DirectX2.cs",
        "src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/RenderControls/Backends/DirectX/SkiaRenderControl_DirectX.cs",
        "src/OngekiFumenEditor.Avalonia/UI/Controls/AnimatedScrollViewer.cs",
        "src/OngekiFumenEditor.Avalonia/Parser/DefaultImpl/Nyageki/CommandImpl/Objects/BulletPalleteCommandParser.cs",
        "src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Graphics/Drawing/Editors/DrawPlayableAreaHelper.cs",
        "src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/Interactives/Impls/DockableObjectInteractiveAction.cs"
    ];

    private static readonly string[] MojibakeMarkers =
    [
        "锟", "�", "鑰", "檻", "", "鐨", "剅", "€",
        "绾", "缂", "鐗", "浠", "灞", "绉", "姣", "鎸",
        "鐢", "瑕", "闅", "棌", "璁", "褰", "锛", "妫",
        "缁", "杞", "纭", "閫", "鏇", "瀹", "绠", "闂",
        "澶", "绐", "閿", "鎺", "濡", "灏", "涓", "鏃",
        "浣", "鍔", "绫", "璺", "搴", "鑾", "婢", "跺",
        "秴", "鍩", "鈺", "崣", "鍌", "殶", "閸", "苯",
        "鍞", "顕", "挒", "娴", "灝", "惄", "顔", "界", "垼"
    ];

    [Fact]
    public void MigratedTextSources_AreStrictUtf8AndFreeOfKnownMojibake()
    {
        var repositoryRoot = FindRepositoryRoot();
        var failures = new List<string>();
        var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        foreach (var relativePath in SourceFiles)
        {
            var path = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string source;
            try
            {
                source = File.ReadAllText(path, strictUtf8);
            }
            catch (Exception exception)
            {
                failures.Add($"{relativePath}: invalid UTF-8 ({exception.Message})");
                continue;
            }

            var lines = source.Split(["\r\n", "\n"], StringSplitOptions.None);
            for (var index = 0; index < lines.Length; index++)
            {
                if (MojibakeMarkers.Any(marker => lines[index].Contains(marker, StringComparison.Ordinal)))
                    failures.Add($"{relativePath}:{index + 1}: {lines[index]}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void PropertySetAction_Name_UsesLocalizedPropertyTemplate()
    {
        var action = new PropertySetAction<int>("XGrid", _ => { }, 0, 1);

        Assert.Equal(Lang.ObjectPropertyChanged.Format("XGrid"), action.Name.Text);
        Assert.DoesNotContain("锟", action.Name.Text, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OngekiFumenEditor.Avalonia.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not find the Avalonia repository root.");
    }
}
