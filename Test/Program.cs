using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Collections;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

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

            var timeGridSize = 240;
            var offsetY = 100;
            var baseTGridDisplay = new TGrid(1, 1920 / 4);
            var height = 1000;
            var pickY = 120;


            //获取baseTGridDisplay对应的当前bpm
            var baseBPM = bpmList.GetBpm(baseTGridDisplay);
            var baseOffsetLen = MathUtils.CalculateBPMLength(baseBPM, baseTGridDisplay, timeGridSize);

            var positionBpmList = new List<(double startY, BPMChange bpm)>();

            {
                double y = offsetY - baseOffsetLen;
                var curBpm = baseBPM;
                while (y <= height)
                {
                    positionBpmList.Add((y, curBpm));
                    var nextBpm = bpmList.GetNextBpm(curBpm);
                    if (nextBpm is null)
                        break;
                    var bpmLen = MathUtils.CalculateBPMLength(curBpm, nextBpm, timeGridSize);
                    y += bpmLen;
                    curBpm = nextBpm;
                }
            }

            if (baseOffsetLen < offsetY)
            {
                //表示可能还需要上一个BPM(如果有的话)参与计算，因为y可能会对应到上一个BPM（即物件可能在基轴下方
                var prevBPM = bpmList.GetPrevBpm(baseBPM);
                if (prevBPM is BPMChange)
                {
                    var bpmLen = MathUtils.CalculateBPMLength(prevBPM, baseBPM, timeGridSize);
                    positionBpmList.Insert(0, (-(offsetY - baseOffsetLen + bpmLen), prevBPM));
                }
            }

            //获取pickY对应的bpm和bpm起始位置
            (var pickStartY, var pickBpm) = positionBpmList.LastOrDefault(x => x.startY <= pickY);
            var relativeBpmLenOffset = pickBpm.LengthConvertToOffset(pickY - pickStartY, timeGridSize);

            var pickTGrid = pickBpm.TGrid + relativeBpmLenOffset;
        }
    }
}
