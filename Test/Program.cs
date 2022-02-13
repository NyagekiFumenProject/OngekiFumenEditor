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
            var startObject = new LaneLeftStart();
            startObject.RecordId = 0;

            startObject.AddChildObject(new LaneLeftNext() { TGrid = new(1) });
            startObject.AddChildObject(new LaneLeftNext() { TGrid = new(2) });
            startObject.AddChildObject(new LaneLeftNext() { TGrid = new(3) });
            startObject.AddChildObject(new LaneLeftNext() { TGrid = new(4) });

            startObject.InsertChildObject(new(0.5f), new LaneLeftNext() { TGrid = new(0.5f) });

            foreach (var item in startObject.GetDisplayableObjects())
            {
                Console.WriteLine(item.ToString());
            }
        }
    }
}
