using Gemini.Modules.UndoRedo;
using Gemini.Modules.UndoRedo.UndoAction;
using ModelContextProtocol.Server;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
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
            public string Shooter;
            public string Target;
            public string Size;
            public string Type;
            public string BulletDamageType;
            public int? PlaceOffset;
            public int? RandomOffsetRange;
            public TGrid EndTGrid;
            public int? MeterBunShi;
            public int? MeterBunbo;
            public string EnemyWave;
            public string LaneType;
            public string SoflanType;
            public double? Speed;
            public int? SoflanGroup;
            public bool? ApplySpeedInDesignMode;
            public LaneStartBase ReferenceLane;
            public bool SnapXToLane;
            public ConnectableStartObject ParentStart;
            public ConnectableChildObjectBase CurveTarget;
            public int? WidthId;
            public XGrid ObliqueSourceXGrid;
            public ColorId? ColorId;
            public int? Brightness;
            public bool? IsTransparent;
            public XGrid EndXGrid;
            public string BlockDirection;
        }

        private static OngekiObjectBase CreateObject(ObjectCreateSpec spec)
        {
            switch (spec.Family)
            {
                case "tap":
                    return CreateTap(spec);

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
                    {
                        var bullet = new Bullet
                        {
                            TGrid = spec.TGrid,
                            XGrid = spec.XGrid,
                            ReferenceBulletPallete = spec.Pallete,
                        };
                        ApplyProjectileCustomFields(bullet, spec, "bullet");
                        return bullet;
                    }

                case "bell":
                    {
                        var bell = new Bell
                        {
                            TGrid = spec.TGrid,
                            XGrid = spec.XGrid,
                            ReferenceBulletPallete = spec.Pallete,
                        };
                        ApplyProjectileCustomFields(bell, spec, "bell");
                        return bell;
                    }

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

                case "lanenext":
                    return CreateConnectableChild(spec, static start => start.CreateChildObject(), "lanenext");

                case "beam":
                    return CreateBeam(spec);

                case "beamnext":
                    return CreateConnectableChild(spec, static start => new BeamNext(), "beamnext");

                case "curvecontrol":
                    return CreateCurvePathControl(spec);

                case "isfarea":
                    return CreateIndividualSoflanArea(spec);

                case "laneblock":
                    return CreateLaneBlockArea(spec);

                default:
                    throw new ArgumentException($"Unsupported object family '{spec.Family}'.");
            }
        }

        /// <summary>
        /// §36/§41/§42：bullet/bell 的 custom projectile 参数只在没有 palette 时生效 ——
        /// 有 palette 时这些值由 palette 派生（属性浏览器里也是只读的）。AddObject 的校验已经拒绝
        /// 「palette + custom 参数」的组合，这里对 palette 模式直接跳过，避免覆盖派生值的语义歧义。
        /// </summary>
        private static void ApplyProjectileCustomFields(IBulletPalleteReferencable projectile, ObjectCreateSpec spec, string familyName)
        {
            if (spec.Pallete is not null)
                return;

            switch (projectile)
            {
                case Bell bell:
                    if (spec.Shooter is not null)
                        bell.ShooterValue = ParsePalleteEnum<Shooter>(spec.Shooter, "shooter");
                    if (spec.Target is not null)
                        bell.TargetValue = ParsePalleteEnum<Target>(spec.Target, "target");
                    if (spec.Speed is { } bellSpeed)
                        bell.Speed = ToFiniteFloat(bellSpeed, "speed");
                    if (spec.PlaceOffset is { } bellPlaceOffset)
                        bell.PlaceOffset = bellPlaceOffset;
                    if (spec.RandomOffsetRange is { } bellRandomOffsetRange)
                        bell.RandomOffsetRange = bellRandomOffsetRange;
                    return;

                case Bullet bullet:
                    if (spec.Shooter is not null)
                        bullet.ShooterValue = ParsePalleteEnum<Shooter>(spec.Shooter, "shooter");
                    if (spec.Target is not null)
                        bullet.TargetValue = ParsePalleteEnum<Target>(spec.Target, "target");
                    if (spec.Speed is { } bulletSpeed)
                        bullet.Speed = ToFiniteFloat(bulletSpeed, "speed");
                    if (spec.PlaceOffset is { } bulletPlaceOffset)
                        bullet.PlaceOffset = bulletPlaceOffset;
                    if (spec.RandomOffsetRange is { } bulletRandomOffsetRange)
                        bullet.RandomOffsetRange = bulletRandomOffsetRange;
                    if (spec.Size is not null)
                        bullet.SizeValue = ParsePalleteEnum<BulletSize>(spec.Size, "size");
                    if (spec.Type is not null)
                        bullet.TypeValue = ParsePalleteEnum<BulletType>(spec.Type, "type");
                    if (spec.BulletDamageType is not null)
                        bullet.BulletDamageTypeValue = ParsePalleteEnum<BulletDamageType>(spec.BulletDamageType, "bulletDamageType");
                    return;

                default:
                    throw new ArgumentException($"A {familyName} has no custom projectile parameters.");
            }
        }

        /// <summary>double → float 保留 soflan 侧同样的有限性校验，避免 NaN/Infinity 写进谱面对象。</summary>
        private static float ToFiniteFloat(double value, string propertyName)
        {
            var result = (float)value;
            if (float.IsNaN(result) || float.IsInfinity(result))
                throw new ArgumentException($"'{value}' is not a finite number for {propertyName}.");
            return result;
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
                "autoplayfader" or "autoplayfaderlane" => new AutoplayFaderLaneStart(),
                _ => throw new ArgumentException($"'{spec.LaneType}' is not a valid laneType; expected center, left, right, colorful, enemy, wallLeft, wallRight or autoplayFader."),
            };

            lane.TGrid = spec.TGrid;
            lane.XGrid = spec.XGrid;

            if (spec.IsTransparent is { } isTransparent)
                lane.IsTransparent = isTransparent;

            // 色带 lane 的颜色/亮度是可配置的，且 add 时不指定就永远是默认的 Akari / 亮度 3。
            if (lane is IColorfulLane colorful)
            {
                if (spec.ColorId is { } colorId)
                    colorful.ColorId = colorId;
                if (spec.Brightness is { } brightness)
                    colorful.Brightness = brightness;
            }
            else if (spec.ColorId is not null || spec.Brightness is not null)
            {
                throw new ArgumentException($"colorId/brightness only apply to laneType 'colorful'; a '{lane.LaneType}' lane has no colour.");
            }

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

            if (spec.EndTGrid is { } endTGrid && endTGrid <= spec.TGrid)
                throw new ArgumentException($"A hold end must be after the hold start (end {endTGrid} <= start {spec.TGrid}).");

            // §58/§62：先绑 lane，再（可选）把起点吸附到 lane。吸附必须在 lane 算不出 XGrid 时直接失败。
            if (spec.ReferenceLane is { } referenceLane)
            {
                hold.ReferenceLaneStart = referenceLane;
                if (spec.SnapXToLane)
                    hold.XGrid = RequireLaneXGrid(referenceLane, hold.TGrid, hold);
            }

            if (spec.EndTGrid is { } holdEndTGrid)
            {
                var holdEnd = new HoldEnd
                {
                    TGrid = holdEndTGrid,
                    XGrid = hold.XGrid,
                };
                // SetHoldEnd 内部会 RedockXGrid()：终点是「lane 能算就跟 lane，算不出就静默保留」，
                // 所以下面还要显式再算一次，把「算不出」升级成校验失败。
                hold.SetHoldEnd(holdEnd);
                if (spec.SnapXToLane && spec.ReferenceLane is { } laneForEnd)
                    holdEnd.XGrid = RequireLaneXGrid(laneForEnd, holdEnd.TGrid, holdEnd);
            }

            return hold;
        }

        private static OngekiObjectBase CreateTap(ObjectCreateSpec spec)
        {
            var tap = new Tap
            {
                TGrid = spec.TGrid,
                XGrid = spec.XGrid,
                IsCritical = spec.IsCritical ?? false,
            };

            if (spec.ReferenceLane is { } referenceLane)
            {
                tap.ReferenceLaneStart = referenceLane;
                if (spec.SnapXToLane)
                    tap.XGrid = RequireLaneXGrid(referenceLane, tap.TGrid, tap);
            }

            return tap;
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

        /// <summary>
        /// lane / beam 的延伸段（lanenext / beamnext）工厂。延伸段必须挂在已有起点上：
        /// 起点由 <c>parentRecordId</c> 解析而来，子类由起点自己的 <c>CreateChildObject()</c> 决定，
        /// 这样 colorful / enemy / wall / autoplayfader 等变体都能自动落到正确的子类型。
        /// </summary>
        private static OngekiObjectBase CreateConnectableChild(ObjectCreateSpec spec, Func<ConnectableStartObject, ConnectableChildObjectBase> childFactory, string familyName)
        {
            var start = spec.ParentStart
                ?? throw new ArgumentException($"The {familyName} family requires parentRecordId: the RecordId of the owning start object.");

            var child = childFactory(start);

            if (spec.EndTGrid is { } forbiddenEnd)
                throw new ArgumentException($"A {familyName} segment is a single point: pass tGridUnit/tGridGrid only (got endTGrid {forbiddenEnd}).");

            child.TGrid = spec.TGrid;
            child.XGrid = spec.XGrid;

            // 子物件靠 ReferenceStartObject / RecordId 归属起点；ConnectableObjectList.Add 也按 RecordId 找父。
            child.SetReferenceStartObject(start);
            child.RecordId = start.RecordId;

            if (child is IBeamObject beam)
            {
                if (spec.WidthId is { } widthId)
                    beam.WidthId = WidthIdConst.AllWidthIds.FirstOrDefault(x => x.Id == widthId)
                        ?? throw new ArgumentException($"'{widthId}' is not a valid widthId; valid ids are {string.Join(", ", WidthIdConst.AllWidthIds.Select(x => x.Id))}.");
                // 斜光束：起点/延伸段都能各自带偏移；这里只在显式给了偏移时写，避免把起点的偏移顶掉。
                if (spec.ObliqueSourceXGrid is { } oblique)
                    beam.ObliqueSourceXGridOffset = oblique;
            }

            // 色带 lane 的延伸段同样带颜色/亮度（ColorfulLaneNext 实现 IColorfulLane）。
            if (child is IColorfulLane colorful)
            {
                if (spec.ColorId is { } colorId)
                    colorful.ColorId = colorId;
                if (spec.Brightness is { } brightness)
                    colorful.Brightness = brightness;
            }
            else if (spec.ColorId is not null || spec.Brightness is not null)
            {
                throw new ArgumentException($"colorId/brightness only apply to a colorful lane; a {familyName} segment on a {start.LaneType} lane has no colour.");
            }

            return child;
        }

        private static OngekiObjectBase CreateBeam(ObjectCreateSpec spec)
        {
            var beam = new BeamStart
            {
                TGrid = spec.TGrid,
                XGrid = spec.XGrid,
            };

            if (spec.WidthId is { } widthId)
                beam.WidthId = WidthIdConst.AllWidthIds.FirstOrDefault(x => x.Id == widthId)
                    ?? throw new ArgumentException($"'{widthId}' is not a valid widthId; valid ids are {string.Join(", ", WidthIdConst.AllWidthIds.Select(x => x.Id))}.");
            if (spec.ObliqueSourceXGrid is { } oblique)
                beam.ObliqueSourceXGridOffset = oblique;

            return beam;
        }

        private static OngekiObjectBase CreateCurvePathControl(ObjectCreateSpec spec)
        {
            var target = spec.CurveTarget
                ?? throw new ArgumentException("The curvecontrol family requires referenceObjectId: the object id of the lane segment the control point bends.");

            return new LaneCurvePathControlObject
            {
                TGrid = spec.TGrid,
                XGrid = spec.XGrid,
            };
        }

        private static OngekiObjectBase CreateIndividualSoflanArea(ObjectCreateSpec spec)
        {
            if (spec.EndTGrid is null)
                throw new ArgumentException("The isfarea family requires endTGridUnit/endTGridGrid: an individual soflan area is a range.");

            if (spec.EndTGrid <= spec.TGrid)
                throw new ArgumentException($"An individual soflan area end must be after its start (end {spec.EndTGrid} <= start {spec.TGrid}).");

            var area = new IndividualSoflanArea
            {
                TGrid = spec.TGrid,
                XGrid = spec.XGrid,
                SoflanGroup = spec.SoflanGroup ?? 0,
            };

            // 区域是「起止矩形」：终点 TGrid 决定纵向长度，终点 XGrid 决定横向宽度（AreaWidth 是派生的）。
            area.EndIndicator.TGrid = spec.EndTGrid;
            area.EndIndicator.XGrid = spec.EndXGrid ?? spec.XGrid;

            return area;
        }

        private static OngekiObjectBase CreateLaneBlockArea(ObjectCreateSpec spec)
        {
            if (spec.EndTGrid is null)
                throw new ArgumentException("The laneblock family requires endTGridUnit/endTGridGrid: a lane block is a range.");

            if (spec.EndTGrid <= spec.TGrid)
                throw new ArgumentException($"A lane block end must be after its start (end {spec.EndTGrid} <= start {spec.TGrid}).");

            var block = new LaneBlockArea
            {
                TGrid = spec.TGrid,
                Direction = ParseLaneBlockDirection(spec.BlockDirection),
            };
            block.EndIndicator.TGrid = spec.EndTGrid;

            return block;
        }

        private static LaneBlockArea.BlockDirection ParseLaneBlockDirection(string raw)
        {
            var text = (raw ?? string.Empty).Trim();
            if (text.Length == 0)
                return LaneBlockArea.BlockDirection.Left;

            return text.ToLowerInvariant() switch
            {
                "left" => LaneBlockArea.BlockDirection.Left,
                "right" => LaneBlockArea.BlockDirection.Right,
                _ => throw new ArgumentException($"'{raw}' is not a valid blockDirection; expected left or right."),
            };
        }
    }
}
