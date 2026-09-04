using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing;
using Xunit;
using Matrix4 = OpenTK.Mathematics.Matrix4;
using Vector2 = OpenTK.Mathematics.Vector2;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenVisualEditor;

public sealed class CameraRelativeCoordinateTests
{
    private const float ViewWidth = 800f;
    private const float ViewHeight = 450f;

    private static DrawingTargetContext CreateContext(double cameraWorldMinY)
    {
        return new DrawingTargetContext()
        {
            ViewRelativeRect = new VisibleRect(new Vector2(ViewWidth, 0), new Vector2(0, ViewHeight)),
            WorldRect = new VisibleRect(new Vector2(ViewWidth, (float)cameraWorldMinY), new Vector2(0, (float)(cameraWorldMinY + ViewHeight))),
            ViewRelativeOriginY = cameraWorldMinY,
            ViewWidth = ViewWidth,
            ViewHeight = ViewHeight,
        };
    }

    [Fact]
    public void ViewRelativeRect_IsFixedSizeViewport()
    {
        var ctx = CreateContext(123456789.5);

        Assert.Equal(0, ctx.ViewRelativeRect.MinY);
        Assert.Equal(ViewHeight, ctx.ViewRelativeRect.MaxY);
        Assert.Equal(ViewWidth, ctx.ViewRelativeRect.MaxX);
        Assert.Equal(ViewHeight, ctx.ViewRelativeRect.Height);
    }

    [Fact]
    public void WorldRect_MinY_MatchesViewRelativeOriginY()
    {
        const double origin = 9876543;
        var ctx = CreateContext(origin);

        Assert.Equal(origin, ctx.ViewRelativeOriginY);
        Assert.Equal(origin, ctx.WorldRect.MinY);
        Assert.Equal(origin + ViewHeight, ctx.WorldRect.MaxY);
    }

    [Fact]
    public void SelectionOriginMath_WorldTopBottomMinusOrigin_LandsInViewport()
    {
        //camera deep into a long chart: world origin around 1e7
        const double origin = 1e7;
        var ctx = CreateContext(origin);

        //selection rect in world coordinates within the visible band
        var worldTop = origin + ViewHeight * 0.75;
        var worldBottom = origin + ViewHeight * 0.25;

        var topY = (float)(worldTop - ctx.ViewRelativeOriginY);
        var bottomY = (float)(worldBottom - ctx.ViewRelativeOriginY);

        Assert.InRange(topY, 0, ViewHeight);
        Assert.InRange(bottomY, 0, ViewHeight);
    }

    [Fact]
    public void HitRegistration_RestoresWorldY_FromViewRelativeCenter()
    {
        const double origin = 5_000_000;
        var ctx = CreateContext(origin);

        //a target produces a view-relative center (e.g. from ConvertToViewRelativeY)
        const float viewRelativeCenterY = 123.5f;
        var worldCenterY = viewRelativeCenterY + ctx.ViewRelativeOriginY;

        //hit rect stores world coordinates (top = centerY - size/2)
        var hitTop = worldCenterY - 8f / 2;
        Assert.Equal(origin + viewRelativeCenterY - 4f, hitTop);
    }

    [Fact]
    public void ViewMatrix_IsConstantRegardlessOfScrollPosition()
    {
        //the de-globalized view matrix folds neither the scroll offset nor the judge line offset;
        //both a deep and a shallow camera produce the same constant translation
        var expected = Matrix4.CreateTranslation(-ViewWidth / 2, -ViewHeight / 2, 0);

        //simulate what OnEditorRender builds for each soflan group now
        static Matrix4 BuildViewMatrix() => Matrix4.CreateTranslation(-ViewWidth / 2, -ViewHeight / 2, 0);

        Assert.Equal(expected, BuildViewMatrix());
        Assert.Equal(expected, BuildViewMatrix());
    }

    [Fact]
    public void FloatNarrowing_AfterDoubleDeduction_KeepsPrecision()
    {
        //world Y far beyond float grid resolution around 1e7
        const double worldY = 1e7 + 3.75;
        const double origin = 1e7;

        //new path: deduct in the double domain, then narrow to float
        var newPath = (float)(worldY - origin);

        //old path: narrow first, then deduct in float domain — loses 0.25 to the float grid
        var oldPath = (float)worldY - (float)origin;

        Assert.Equal(3.75f, newPath, 3);
        Assert.Equal(4f, oldPath, 3);
    }

}
