using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
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
            bpmList.Add(new() { TGrid = new(1, 0), BPM = 240 });
            bpmList.Add(new() { TGrid = new(2, 0), BPM = 240 });
            bpmList.Add(new() { TGrid = new(3, 0), BPM = 480 });
            bpmList.Add(new() { TGrid = new(4, 0), BPM = 100 });

            var meterList = new MeterChangeList();
            meterList.Add(new() { TGrid = new(0, 1920 / 2) });
            meterList.Add(new() { TGrid = new(2, 0) });

            var timeSignatures = meterList.GetCachedAllTimeSignatureUniformPositionList(240, bpmList);

            foreach (var timeSignature in timeSignatures)
            {
                Console.WriteLine($"{timeSignature.startY:F2} ({timeSignature.bpm}) ({timeSignature.meter})");
            }
        }
    }
}
