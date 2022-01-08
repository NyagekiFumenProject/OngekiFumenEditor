using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Collections;
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
            bpmList.Add(new() { TGrid = new(1, 0), BPM = 480 });
            bpmList.Add(new() { TGrid = new(2, 0), BPM = 240 });
            bpmList.Add(new() { TGrid = new(3, 0), BPM = 480 });
            bpmList.Add(new() { TGrid = new(4, 0), BPM = 240 });

            while (true)
            {
                var result = new Dictionary<BPMChange, double>();
                var offsetY = 240d;
                var s = Console.ReadLine().Split();
                var currentTime = new TGrid(int.Parse(s[0]), int.Parse(s[1]));
                Console.Clear();
                Console.WriteLine($"currentTime : {currentTime}");
                var baseBPM = bpmList.GetBpm(currentTime);
                var baseOffsetLen = MathUtils.CalculateBPMLength(baseBPM, currentTime, offsetY);

                result[bpmList.FirstBpm] = 0;
                var prev = bpmList.FirstBpm;
                var y = 0d;
                var totalOffset = 0d;

                if (bpmList.FirstBpm == baseBPM)
                {
                    totalOffset = -(50 - baseOffsetLen);
                    Console.WriteLine($"pre-totalOffset : {totalOffset}");
                    foreach (var bpm in result.Keys)
                    {
                        result[bpm] -= totalOffset;
                    }
                }

                while (true)
                {
                    var cur = bpmList.GetNextBpm(prev);
                    if (cur is null)
                        break;
                    var len = MathUtils.CalculateBPMLength(prev, cur.TGrid, offsetY);
                    prev = cur;
                    y += len;
                    result[cur] = y - totalOffset;

                    if (cur == baseBPM)
                    {
                        totalOffset = baseOffsetLen + y;
                        Console.WriteLine($"totalOffset : {totalOffset}");
                        foreach (var bpm in result.Keys)
                        {
                            result[bpm] -= totalOffset;
                        }
                    }
                }

                foreach (var item in result)
                {
                    Console.WriteLine($"{item.Value} - {item.Key}");
                }
            }
        }
    }
}
