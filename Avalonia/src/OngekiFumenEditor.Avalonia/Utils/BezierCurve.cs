using System.Collections.Generic;
using System.Numerics;

namespace OngekiFumenEditor.Avalonia.Utils;

public static class BezierCurve
{
    // De Casteljau 算法，等价于 OpenTK.Mathematics.BezierCurve.CalculatePoint(IList<Vector2>, float)
    public static Vector2 CalculatePoint(IList<Vector2> points, float t)
    {
        var count = points.Count;
        var temp = new Vector2[count];
        for (var i = 0; i < count; i++)
            temp[i] = points[i];

        for (var level = count - 1; level > 0; level--)
        {
            for (var i = 0; i < level; i++)
                temp[i] = (1 - t) * temp[i] + t * temp[i + 1];
        }

        return temp[0];
    }
}
