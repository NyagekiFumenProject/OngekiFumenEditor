using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Test
{
    class Program
    {
        static IEnumerable<int> GetInt(double from, double to)
        {
            var sign = Math.Sign(to - from);
            var begin = 0;
            var end = 0;

            if (sign > 0)
            {
                begin = (int)Math.Ceiling(from);
                end = (int)Math.Floor(to);
            }
            if (sign < 0)
            {
                begin = (int)Math.Floor(from);
                end = (int)Math.Ceiling(to);
            }

            for (int i = begin; sign > 0 ? i <= end : i >= end; i += sign)
                yield return i;
        }

        static void Main(string[] args)
        {
            Console.WriteLine(string.Join(" , ", GetInt(0.5, 6.5)));
            Console.WriteLine(string.Join(" , ", GetInt(-0.5, -6.5)));
            Console.WriteLine(string.Join(" , ", GetInt(6.5, -6.5)));
            Console.WriteLine(string.Join(" , ", GetInt(-6.5, 6.5)));
            Console.WriteLine(string.Join(" , ", GetInt(-0.023129463, 0.5279732)));
        }
    }
}
