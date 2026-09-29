using Gemini.Modules.UndoRedo;
using Gemini.Modules.UndoRedo.UndoAction;
using ModelContextProtocol.Server;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Kernel.RuntimeAutomation;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Mcp.Tools.Editor
{
    internal sealed partial class EditorMutationTool
    {
        /// <summary>editor.add_object 的入参集合：家族相关的可选参数很多，聚合成一个 spec 传递。</summary>
        private sealed class ObjectCreateSpec
        {
            public string Family;
            public TGrid TGrid;
            public XGrid XGrid;
            public bool? IsCritical;
            public string Direction;
            public string Content;
            public double? Bpm;
            public BulletPallete Pallete;
            public TGrid EndTGrid;
            public int? MeterBunShi;
            public int? MeterBunbo;
            public string EnemyWave;
            public string LaneType;
            public string SoflanType;
            public double? Speed;
            public int? SoflanGroup;
            public bool? ApplySpeedInDesignMode;
        }

        private static OngekiObjectBase CreateObject(ObjectCreateSpec spec)
        {
            switch (spec.Family)
            {
                case "tap":
                    return new Tap
                    {
                        TGrid = spec.TGrid,
                        XGrid = spec.XGrid,
                        IsCritical = spec.IsCritical ?? false,
                    };

                case "flick":
                    return new Flick
                    {
                        TGrid = spec.TGrid,
                        XGrid = spec.XGrid,
                        Direction = ParseDirection(spec.Direction),
                        IsCritical = spec.IsCritical ?? false,
                    };

                case "comment":
                    return new Comment
                    {
                        TGrid = spec.TGrid,
                        Content = spec.Content ?? string.Empty,
                    };

                case "bpm":
                    if (spec.Bpm is not { } bpmValue || bpmValue <= 0)
                        throw new ArgumentException("The bpm family requires a positive 'bpm' value.");

                    return new BPMChange
                    {
                        TGrid = spec.TGrid,
                        BPM = bpmValue,
                    };

                case "bullet":
                    return new Bullet
                    {
                        TGrid = spec.TGrid,
                        XGrid = spec.XGrid,
                        ReferenceBulletPallete = spec.Pallete,
                    };

                case "bell":
                    return new Bell
                    {
                        TGrid = spec.TGrid,
                        XGrid = spec.XGrid,
                        ReferenceBulletPallete = spec.Pallete,
                    };

                case "meter":
                    {
                        var meter = new MeterChange
                        {
                            TGrid = spec.TGrid,
                            BunShi = spec.MeterBunShi ?? 4,
                            Bunbo = spec.MeterBunbo ?? 4,
                        };
                        if (meter.BunShi <= 0 || meter.Bunbo <= 0)
                            throw new ArgumentException("meterBunShi and meterBunbo must be positive.");
                        return meter;
                    }

                case "clickse":
                    return new ClickSE { TGrid = spec.TGrid };

                case "enemy":
                    return new EnemySet
                    {
                        TGrid = spec.TGrid,
                        TagTblValue = ParsePalleteEnum<EnemySet.WaveChangeConst>(spec.EnemyWave ?? "Boss", "enemyWave"),
                    };

                case "lane":
                    return CreateLane(spec);

                case "hold":
                    return CreateHold(spec);

                case "soflan":
                    return CreateSoflan(spec);

                default:
                    throw new ArgumentException($"Unsupported object family '{spec.Family}'.");
            }
        }

        private static OngekiObjectBase CreateLane(ObjectCreateSpec spec)
        {
            LaneStartBase lane = (spec.LaneType ?? "center").Trim().ToLowerInvariant() switch
            {
                "center" => new LaneCenterStart(),
                "left" => new LaneLeftStart(),
                "right" => new LaneRightStart(),
                "colorful" => new ColorfulLaneStart(),
                "enemy" => new EnemyLaneStart(),
                "wallleft" => new WallLeftStart(),
                "wallright" => new WallRightStart(),
                _ => throw new ArgumentException($"'{spec.LaneType}' is not a valid laneType; expected center, left, right, colorful, enemy, wallLeft or wallRight."),
            };

            lane.TGrid = spec.TGrid;
            lane.XGrid = spec.XGrid;
            return lane;
        }

        private static OngekiObjectBase CreateHold(ObjectCreateSpec spec)
        {
            var hold = new Hold
            {
                TGrid = spec.TGrid,
                XGrid = spec.XGrid,
                IsCritical = spec.IsCritical ?? false,
            };

            if (spec.EndTGrid is { } endTGrid)
            {
                if (endTGrid <= spec.TGrid)
                    throw new ArgumentException($"A hold end must be after the hold start (end {endTGrid} <= start {spec.TGrid}).");

                var holdEnd = new HoldEnd
                {
                    TGrid = endTGrid,
                    XGrid = spec.XGrid,
                };
                hold.SetHoldEnd(holdEnd);
            }

            return hold;
        }

        private static OngekiObjectBase CreateSoflan(ObjectCreateSpec spec)
        {
            var kind = (spec.SoflanType ?? "duration").Trim().ToLowerInvariant();
            var isKeyframe = kind is "keyframe";

            // KeyframeSoflan 是「一个点」：它的 EndTGrid 就是 TGrid（KeyframeSoflan.cs），
            // 所以这里用 tGrid* 定位，endTGrid* 可省略，给了就必须与 tGrid* 相等。
            if (!isKeyframe && spec.EndTGrid is null)
                throw new ArgumentException("The soflan family requires endTGridUnit/endTGridGrid: a duration/interpolatable soflan is a range.");

            // KeyframeSoflan 是「一个点」：它没有 end（其 EndTGrid 就是 TGrid），因此禁止传 endTGrid*。
            if (isKeyframe && spec.EndTGrid is not null)
                throw new ArgumentException("A keyframe soflan is a single point and has no end: pass tGridUnit/tGridGrid only.");

            if (!isKeyframe && spec.EndTGrid is { } endTGrid && endTGrid <= spec.TGrid)
                throw new ArgumentException($"A soflan end must be after its start (end {endTGrid} <= start {spec.TGrid}).");

            var speed = (float)(spec.Speed ?? 1);
            if (double.IsNaN(speed) || double.IsInfinity(speed))
                throw new ArgumentException("'speed' must be a finite number.");

            ISoflan soflan = kind switch
            {
                "duration" or "soflan" => new Soflan(),
                "interpolatable" => new InterpolatableSoflan(),
                "keyframe" => new KeyframeSoflan(),
                _ => throw new ArgumentException($"'{spec.SoflanType}' is not a valid soflanType; expected duration, interpolatable or keyframe."),
            };

            soflan.TGrid = spec.TGrid;
            if (!isKeyframe && spec.EndTGrid is { } end)
                soflan.EndTGrid = end;
            soflan.Speed = speed;
            if (spec.SoflanGroup is { } group)
                soflan.SoflanGroup = group;
            if (spec.ApplySpeedInDesignMode is { } apply)
                soflan.ApplySpeedInDesignMode = apply;

            return (OngekiObjectBase)soflan;
        }
    }
}
