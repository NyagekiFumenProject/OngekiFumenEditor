using Caliburn.Micro;
using OngekiFumenEditor;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
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
            var sortList = new SortableCollection<TGrid, TGrid>(x => x)
            {
                new TGrid(0,0),
                new TGrid(1,0),
                new TGrid(2,0),
                new TGrid(3,0),
                new TGrid(4,0),
                new TGrid(5,0),
                new TGrid(6,0),
                new TGrid(7,0),
                new TGrid(8,0),
                new TGrid(9,0)
            };

            foreach (var item in sortList.BinaryFindRange(new(0, 0), new(0, 0)))
            {
                Console.WriteLine(item);
            }

            sortList = new SortableCollection<TGrid, TGrid>(x => x)
            {
                new TGrid(5,0),
            };

            foreach (var item in sortList.BinaryFindRange(new(4, 0), new(5, 0)))
            {
                Console.WriteLine(item);
            }
        }
    }
}
