using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.SvgToLaneBrowser
{
    public static class PointReforger
    {
        public static IEnumerable<IEnumerable<PointF>> ReforgeAsUnidirectional(IEnumerable<PointF> lineSegment)
        {
            float calcGradient(PointF a, PointF b)
            {
                if (a.Y == b.Y)
                    return float.MaxValue;

                return (a.Y - b.Y) / (a.X - b.X);
            }

            var list = new List<PointF>();
            var prevPoint = lineSegment.FirstOrDefault();
            var prevSign = 0;

            foreach (var point in lineSegment)
            {
                var gradient = calcGradient(prevPoint, point);
                var sign = MathF.Sign(gradient);

                if (prevSign != sign && list.Count != 0)
                {
                    yield return list;
                    list = new List<PointF>();
                    list.Add(prevPoint);
                }

                prevPoint = point;
                prevSign = sign;

                list.Add(point);
            }

            if (list.Count != 0)
                yield return list;
        }
    }
}
