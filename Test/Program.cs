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
            foreach (var subs in new[] { 1, 2, 3, 4, 5, 4, 3, 2, 1, 1, 2, 3, 4, 5 }.SplitByTurningGradient(x => x))
            {
                Console.WriteLine(string.Join(" ",subs));
            }
        }
    }
}
