using System.Numerics;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.SVG;

// SVG prefabs are temporarily disabled in FumenVisualEditor.
// [RegisterSingleton<IFumenEditorDrawingTarget>]
public sealed class SvgObjectDrawingTarget : CommonDrawTargetBase<SvgPrefabBase>
{
    private static readonly Vector2 MinimumSelectableSize = new(16, 16);

    public override IEnumerable<string> DrawTargetID { get; } =
        [SvgStringPrefab.CommandName, SvgImageFilePrefab.CommandName];

    public override DrawingVisible DefaultVisible => DrawingVisible.Design;
    public override int DefaultRenderOrder => 1000;

    // SVG prefab rendering remains disabled; ISvgDrawing has no command-list counterpart yet.
    public override void Initialize(IRenderManagerImpl impl)
    {
    }

    public override void Draw(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, SvgPrefabBase obj)
    {
        var x = (float)XGridCalculator.ConvertXGridToX(obj.XGrid, target.Editor);
        var soflanList = target.Editor._cacheSoflanGroupRecorder.GetCache(obj);
        var y = (float)target.ConvertToViewRelativeY(obj.TGrid, soflanList);
        var position = new Vector2(x, y);

        var bounds = obj.SourceBounds;
        var renderedSize = new Vector2(Math.Abs(bounds.Width * obj.Scale), Math.Abs(bounds.Height * obj.Scale));
        var selectSize = Vector2.Max(renderedSize, MinimumSelectableSize);

        builder.DrawCircle(position, obj.IsSelected ? new Vector4(1, 1, 0, 1) : new Vector4(0, 1, 0, 0.8f), false, obj.IsSelected ? 8 : 6, 2);

        target.RegisterSelectableObject(obj, position, selectSize);
    }
}
