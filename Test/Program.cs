using Caliburn.Micro;
using OngekiFumenEditor;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var rand = new Random();
            var sw = new Stopwatch();
            TGrid gen() => new(0, rand.Next() % 1920);

            var list = new SortableCollection<TGrid, TGrid>(x => x);
            sw.Restart();
            list.BeginBatchAction();
            for (int i = 0; i < 50; i++)
                list.Add(gen());
            list.EndBatchAction();
            var costTime = sw.ElapsedMilliseconds;
            Console.WriteLine($"Cost : {costTime} ms");

            /*
            foreach (var t in list)
                Console.WriteLine(t);
            */
        }
    }
}
