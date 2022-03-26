using Caliburn.Micro;
using OngekiFumenEditor;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.PrimitiveValue;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Utils;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var p1 = new Vector2(30f, 0f);
            var p2 = new Vector2(500f, 50f);
            var p3 = new Vector2(300f, 60f);

            var bezierCurve = new BezierCurve(p1, p2, p3);
            var length = bezierCurve.CalculateLength(0.1f);

            var list = new List<Vector2>();
            for (var i = 0f; i <= 1; i += 0.01f)
            {
                list.Add(bezierCurve.CalculatePoint(i));
            }

            using var image = new Bitmap(1000, 1000);
            using var graphics = Graphics.FromImage(image);
            using var pen = new Pen(Color.LightGreen);

            graphics.DrawLines(pen, list.Select(x => new PointF(x.X, x.Y)).ToArray());
            graphics.DrawRectangle(pen, new Rectangle((int)p1.X, (int)p1.Y, 3, 3));
            graphics.DrawRectangle(pen, new Rectangle((int)p2.X, (int)p2.Y, 3, 3));
            graphics.DrawRectangle(pen, new Rectangle((int)p3.X, (int)p3.Y, 3, 3));

            graphics.Dispose();
            image.Save(@"F:\zz.png", ImageFormat.Png);
        }
    }
}
