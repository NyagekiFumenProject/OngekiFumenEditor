using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Collections;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using static OngekiFumenEditor.Base.OngekiObjects.EnemySet;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var bpmList = new BpmList();

            bpmList.SetFirstBpm(new() { TGrid = new(0, 0), BPM = 240 });
            bpmList.Add(new() { TGrid = new(1, 0), BPM = 6000 });
            bpmList.Add(new() { TGrid = new(2, 0), BPM = 240 });
            bpmList.Add(new() { TGrid = new(3, 0), BPM = 480 });
            bpmList.Add(new() { TGrid = new(4, 0), BPM = 100 });
        }
    }
}
