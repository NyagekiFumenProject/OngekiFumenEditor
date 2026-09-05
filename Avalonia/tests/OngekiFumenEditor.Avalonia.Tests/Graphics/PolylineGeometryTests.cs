using System.Collections.ObjectModel;
using Polyline2DCSharp;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Graphics;

public sealed class PolylineGeometryTests
{
    [Fact]
    public void Create_AllJointAndEndCapStylesEmitOnlyFiniteTriangles()
    {
        Vec2[] points = [new(0, 0), new(30, 0), new(30, 20), new(10, 35)];

        foreach (var jointStyle in Enum.GetValues<Polyline2D.JointStyle>())
        foreach (var endCapStyle in Enum.GetValues<Polyline2D.EndCapStyle>())
        {
            IList<Vec2> output = new Collection<Vec2>();

            var result = Polyline2D.Create(output, points, 8, jointStyle, endCapStyle);

            Assert.Same(output, result);
            Assert.NotEmpty(result);
            Assert.Equal(0, result.Count % 3);
            Assert.All(result, point => Assert.True(Vec2.isFinite(point),
                $"{jointStyle}/{endCapStyle} emitted ({point.x}, {point.y})."));
        }
    }

    [Fact]
    public void Create_InvalidOrOverflowingGeometryLeavesExistingOutputUntouched()
    {
        var sentinel = new Vec2(7, 11);
        Vec2[][] invalidPoints =
        [
            [new(0, 0)],
            [new(0, 0), new(float.NaN, 1)],
            [new(0, 0), new(1, float.PositiveInfinity)]
        ];

        foreach (var points in invalidPoints)
        {
            IList<Vec2> output = new Collection<Vec2> { sentinel };
            Polyline2D.Create(output, points, 16, Polyline2D.JointStyle.ROUND, Polyline2D.EndCapStyle.ROUND);
            Assert.Equal([sentinel], output);
        }

        IList<Vec2> overflowOutput = new Collection<Vec2> { sentinel };
        Polyline2D.Create(
            overflowOutput,
            [new(float.MaxValue, float.MaxValue), new(-float.MaxValue, -float.MaxValue)],
            float.MaxValue,
            Polyline2D.JointStyle.MITER,
            Polyline2D.EndCapStyle.SQUARE);
        Assert.Equal([sentinel], overflowOutput);

        foreach (var thickness in new[] { 0, -1, float.NaN, float.PositiveInfinity })
        {
            IList<Vec2> output = new Collection<Vec2> { sentinel };
            Polyline2D.Create(output, [new(0, 0), new(10, 10)], thickness);
            Assert.Equal([sentinel], output);
        }
    }

    [Fact]
    public void Intersection_ExtremeFiniteSegmentsStillProducesFinitePoint()
    {
        var extent = float.MaxValue / 2;
        var horizontal = new LineSegment(new(-extent, 0), new(extent, 0));
        var vertical = new LineSegment(new(0, -extent), new(0, extent));

        var intersection = LineSegment.intersection(horizontal, vertical, infiniteLines: false);

        Assert.NotNull(intersection);
        Assert.True(Vec2.isFinite(intersection.Value));
        Assert.Equal(0, intersection.Value.x);
        Assert.Equal(0, intersection.Value.y);
    }
}
