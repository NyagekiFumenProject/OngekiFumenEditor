using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    public static class Vector2ExtensionMethod
    {
        public static OpenTK.Mathematics.Vector2 ToOpenTKVector2(this System.Numerics.Vector2 p) 
            => new (p.X, p.Y);

        public static System.Numerics.Vector2 ToSystemNumericsVector2(this OpenTK.Mathematics.Vector2 p)
            => new (p.X, p.Y);

        public static System.Numerics.Vector2 ToSystemNumericsVector2(this System.Windows.Point p)
            => new((float)p.X, (float)p.Y);
    }
}
