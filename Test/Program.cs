using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Utils;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            /*
            var bpmList = new BpmList();
            bpmList.SetFirstBpm(new BPMChange() { BPM = 120 });
            var meterList = new MeterChangeList()
            {
                new MeterChange(){
                    TGrid = new TGrid(0,0),
                    Bunbo = 4,
                    BunShi = 4,
                }
            };
            var beatSplit = 1;

            foreach (var z in TGridCalculator.GetVisbleTimelines(bpmList, meterList, 0, 1920, 0, beatSplit))
                Console.WriteLine(z);

            void output(float y)
            {
                Console.WriteLine();
                var result = TGridCalculator.TryPickClosestBeatTime(y, bpmList, meterList, beatSplit);
                Console.WriteLine($"y={y} ->  {result} ({result.y - y})");
            }

            output(1500);
            output(990);
            output(1430);

            output(8888);
            */
        }
    }
}
