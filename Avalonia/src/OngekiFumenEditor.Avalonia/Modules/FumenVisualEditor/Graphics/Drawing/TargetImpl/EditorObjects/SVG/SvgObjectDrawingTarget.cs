using System.Numerics;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.SVG;

[RegisterSingleton<IFumenEditorDrawingTarget>]
public sealed class SvgObjectDrawingTarget : CommonDrawTargetBase<SvgPrefabBase>
{
    private static readonly Vector2 MinimumSelectableSize = new(16, 16);
    private ISvgDrawing svgDrawing = null!;
    private ICircleDrawing circleDrawing = null!;

    public override IEnumerable<string> DrawTargetID { get; } =
        [SvgStringPrefab.CommandName, SvgImageFilePrefab.CommandName];

    public override DrawingVisible DefaultVisible => DrawingVisible.Design;
    public override int DefaultRenderOrder => 1000;

    public override void Initialize(IRenderManagerImpl impl)
    {
        svgDrawing = impl.SvgDrawing;
        circleDrawing = impl.CircleDrawing;
    }

    public override void Draw(IFumenEditorDrawingContext target, SvgPrefabBase obj)
    {
        var x = (float)XGridCalculator.ConvertXGridToX(obj.XGrid, target.Editor);
        var soflanList = target.Editor._cacheSoflanGroupRecorder.GetCache(obj);
        var y = (float)target.ConvertToY(obj.TGrid, soflanList);
        var position = new Vector2(x, y);

        svgDrawing.Draw(target, obj, position);

        circleDrawing.Begin(target);
        circleDrawing.Post(position, obj.IsSelected ? new Vector4(1, 1, 0, 1) : new Vector4(0, 1, 0, 0.8f), false, obj.IsSelected ? 8 : 6, 2);
        circleDrawing.End();

        var bounds = obj.SourceBounds;
        var renderedSize = new Vector2(Math.Abs(bounds.Width * obj.Scale), Math.Abs(bounds.Height * obj.Scale));
        target.RegisterSelectableObject(obj, position, Vector2.Max(renderedSize, MinimumSelectableSize));
    }
}
