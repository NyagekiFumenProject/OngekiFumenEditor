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
        private static object BuildBulletPalleteDetail(OngekiFumen fumen, BulletPallete pallete, bool includeReferencingObjects, int? referenceCount = default)
        {
            var referencing = includeReferencingObjects
                ? EnumeratePalleteReferences(fumen).Where(x => PalleteMatches(x.ReferenceBulletPallete, pallete)).ToArray()
                : default;

            return new
            {
                strId = pallete.StrID,
                runtimeId = BulletPalleteList.ConvertIdToInt(pallete.StrID),
                editorName = pallete.EditorName,
                shooter = pallete.ShooterValue.ToString(),
                target = pallete.TargetValue.ToString(),
                size = pallete.SizeValue.ToString(),
                type = pallete.TypeValue.ToString(),
                speed = pallete.Speed,
                placeOffset = pallete.PlaceOffset,
                randomOffsetRange = pallete.RandomOffsetRange,
                isEnableSoflan = pallete.IsEnableSoflan,
                referenceCount = referenceCount ?? referencing?.Length ?? 0,
                referencingObjectIds = referencing?.Select(x => (x as OngekiObjectBase)?.Id ?? 0).Take(50).ToArray(),
            };
        }

        private static IEnumerable<IBulletPalleteReferencable> EnumeratePalleteReferences(OngekiFumen fumen)
            => fumen.Bells.OfType<IBulletPalleteReferencable>().Concat(fumen.Bullets);

        private static bool PalleteMatches(BulletPallete candidate, BulletPallete pallete)
            => candidate is not null && string.Equals(candidate.StrID, pallete.StrID, StringComparison.OrdinalIgnoreCase);

        private static Dictionary<string, int> CountPalleteReferences(OngekiFumen fumen)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var reference in EnumeratePalleteReferences(fumen))
            {
                var id = reference.ReferenceBulletPallete?.StrID;
                if (string.IsNullOrEmpty(id))
                    continue;
                counts[id] = counts.GetValueOrDefault(id) + 1;
            }
            return counts;
        }

        private static bool IsValidPalleteStrId(string strId)
            => strId.Length > 0 && strId.All(char.IsAsciiLetterOrDigit);

        private static bool TryFindObject(OngekiFumen fumen, int objectId, out string family, out OngekiObjectBase obj)
        {
            foreach (var name in QuerableFamilies)
            {
                var hit = EnumerateFamily(fumen, name, TGrid.MinValue, TGrid.MaxValue)?.FirstOrDefault(x => x.Id == objectId);
                if (hit is not null)
                {
                    family = name;
                    obj = hit;
                    return true;
                }
            }

            family = default;
            obj = default;
            return false;
        }

        private static IEnumerable<OngekiObjectBase> EnumerateFamily(OngekiFumen fumen, string family, TGrid min, TGrid max)
        {
            switch (family)
            {
                case "tap":
                    return RangeOf(fumen.Taps, min, max);
                case "flick":
                    return RangeOf(fumen.Flicks, min, max);
                case "bell":
                    return RangeOf(fumen.Bells, min, max);
                case "bullet":
                    return RangeOf(fumen.Bullets, min, max);
                case "comment":
                    return RangeOf(fumen.Comments, min, max);
                case "clickse":
                    return RangeOf(fumen.ClickSEs, min, max);
                case "enemy":
                    return RangeOf(fumen.EnemySets, min, max);
                case "bpm":
                    return RangeOf(fumen.BpmList, min, max);
                case "meter":
                    // MeterChanges.BinaryFindRange 只查 changedMeterList，**不含 FirstMeter**；
                    // 但「列出全部节拍」显然不该漏掉 T=0 的首个节拍（枚举器与 Count 都把它算在内），这里补上。
                    return fumen.MeterChanges.BinaryFindRange(min, max)
                        .Cast<OngekiObjectBase>()
                        .Concat(InRange(fumen.MeterChanges.FirstMeter, min, max)
                            ? new OngekiObjectBase[] { fumen.MeterChanges.FirstMeter }
                            : Enumerable.Empty<OngekiObjectBase>())
                        .Distinct()
                        .OrderBy(x => ((MeterChange)x).TGrid.TotalGrid);
                case "hold":
                    return fumen.Holds.Where(x => InRange(x, min, max));
                case "lane":
                    return fumen.Lanes.Where(x => InRange(x, min, max));
                case "lanenext":
                    // Lanes 集合只枚举起点；延伸段在各自起点的 Children 里。
                    return Ordered(fumen.Lanes.SelectMany(x => x.Children).Where(x => InRange(x, min, max)));
                case "beam":
                    return fumen.Beams.Where(x => InRange(x, min, max));
                case "beamnext":
                    return Ordered(fumen.Beams.SelectMany(x => x.Children).Where(x => InRange(x, min, max)));
                case "curvecontrol":
                    return Ordered(fumen.Lanes
                        .SelectMany(x => x.Children)
                        .SelectMany(x => x.PathControls)
                        .Where(x => InRange(x, min, max)));
                case "isfarea":
                    return Ordered(fumen.IndividualSoflanAreaMap.Values.SelectMany(x => x).Where(x => InRange(x, min, max)));
                case "laneblock":
                    return fumen.LaneBlocks.Where(x => InRange(x, min, max));
                case "soflan":
                    return fumen.SoflansMap.Values.SelectMany(x => x).OfType<OngekiObjectBase>().Where(x => InRange(x, min, max));
                default:
                    return default;
            }
        }

        /// <summary>
        /// 子物件是从各个起点聚合出来的，整体未必按 TGrid 有序；查询分页依赖稳定顺序，
        /// 所以这几族统一按 (TGrid, Id) 排一次。起点族保持原有顺序不动，避免改变既有行为。
        /// </summary>
        private static IEnumerable<OngekiObjectBase> Ordered(IEnumerable<OngekiObjectBase> source)
            => source.OrderBy(x => ((ITimelineObject)x).TGrid.TotalGrid).ThenBy(x => x.Id);

        private static IEnumerable<OngekiObjectBase> RangeOf<T>(IBinaryFindRangeEnumable<T, TGrid> list, TGrid min, TGrid max)
            where T : OngekiObjectBase, ITimelineObject
            => list.BinaryFindRange(min, max).Cast<OngekiObjectBase>();

        private static bool InRange(OngekiObjectBase obj, TGrid min, TGrid max)
            => obj is ITimelineObject timeline && min <= timeline.TGrid && timeline.TGrid <= max;

        /// <summary>§30：辅助对象（displayable helper）——目前只有曲线控制点。</summary>
        private static bool IsAuxiliaryObject(OngekiObjectBase obj)
            => obj is LaneCurvePathControlObject;

        private static object ToObjectDto(string family, OngekiObjectBase obj)
        {
            var tGrid = (obj as ITimelineObject)?.TGrid;
            var xGrid = (obj as OngekiMovableObjectBase)?.XGrid;
            var beam = obj as IBeamObject;
            // §30：辅助对象目前只有曲线控制点；owner 是它在链上的宿主延伸段。
            var curveControl = obj as LaneCurvePathControlObject;
            var curveOwner = curveControl?.RefCurveObject;

            return new
            {
                id = obj.Id,
                type = family,
                tGrid = tGrid is null ? null : new { unit = tGrid.Unit, grid = tGrid.Grid, totalGrid = tGrid.TotalGrid },
                xGrid = xGrid is null ? null : new { unit = xGrid.Unit, grid = xGrid.Grid, totalGrid = xGrid.TotalGrid },
                isCritical = obj is ICriticalableObject criticalable ? criticalable.IsCritical : (bool?)null,
                bulletPalleteStrId = obj is IBulletPalleteReferencable referencable ? referencable.ReferenceBulletPallete?.StrID : default,
                // §58：lane 引用在 MCP 里统一报告为 referenceLaneRecordId，滞空时为 null。
                referenceLaneRecordId = obj is ILaneDockable dockable && dockable.ReferenceLaneStrId >= 0 ? dockable.ReferenceLaneStrId : (int?)null,
                meterBunShi = (obj as MeterChange)?.BunShi,
                meterBunbo = (obj as MeterChange)?.Bunbo,
                // bpm 变化的取值：没有它就只能列出 bpm 变化而读不到 BPM。
                bpm = obj is BPMChange bpmChange ? bpmChange.BPM : (double?)null,
                enemyWave = obj is EnemySet enemy ? enemy.TagTblValue.ToString() : default,
                laneType = obj is ConnectableObjectBase connectable ? connectable.LaneType.ToString() : default,
                // 起点（lane / beam）的 RecordId 就是 parentRecordId 的取值来源；延伸段自带父起点。
                recordId = obj is ConnectableStartObject startObj ? startObj.RecordId : (int?)null,
                parentRecordId = obj is ConnectableChildObjectBase childObj ? childObj.ReferenceStartObject?.RecordId : (int?)null,
                hasHoldEnd = obj is Hold holdObj ? holdObj.HoldEnd is not null : (bool?)null,
                // 光束：宽度档位与斜光束源偏移（斜光束的 IDShortName 是 OBS）。
                widthId = beam?.WidthId.Id,
                obliqueSourceXGrid = (beam?.ObliqueSourceXGridOffset) is { } oblique
                    ? (object)new { unit = oblique.Unit, grid = oblique.Grid, totalGrid = oblique.TotalGrid }
                    : null,
                isObliqueBeam = obj switch
                {
                    BeamStart beamStart => beamStart.IsObliqueBeam,
                    BeamNext beamNext => beamNext.IsObliqueBeam,
                    _ => (bool?)null,
                },
                // 色带 lane 的颜色与亮度。
                colorId = obj is IColorfulLane colorful ? colorful.ColorId.Id : (int?)null,
                colorName = obj is IColorfulLane colorfulNamed ? colorfulNamed.ColorId.Name : default,
                brightness = obj is ColorfulLaneStart colorfulStart ? colorfulStart.Brightness : (int?)null,
                // 曲线控制点：Index 是它在所属延伸段上的顺序；owner* 指向该延伸段，isAuxiliary 标记辅助对象。
                isAuxiliary = IsAuxiliaryObject(obj),
                segmentIndex = curveControl?.Index,
                ownerObjectId = curveOwner?.Id,
                ownerObjectType = curveOwner switch
                {
                    BeamNext => "beamnext",
                    not null => "lanenext",
                    _ => default(string),
                },
                // lane block：Left / Right。
                blockDirection = obj is LaneBlockArea block ? block.Direction.ToString() : default,
                areaWidth = obj is IndividualSoflanArea area ? area.AreaWidth : (float?)null,
                endTGrid = obj switch
                {
                    Hold h when h.HoldEnd is { } he => new { unit = he.TGrid.Unit, grid = he.TGrid.Grid, totalGrid = he.TGrid.TotalGrid },
                    KeyframeSoflan => null,
                    ISoflan s => new { unit = s.EndTGrid.Unit, grid = s.EndTGrid.Grid, totalGrid = s.EndTGrid.TotalGrid },
                    IndividualSoflanArea a => new { unit = a.EndIndicator.TGrid.Unit, grid = a.EndIndicator.TGrid.Grid, totalGrid = a.EndIndicator.TGrid.TotalGrid },
                    LaneBlockArea b => new { unit = b.EndIndicator.TGrid.Unit, grid = b.EndIndicator.TGrid.Grid, totalGrid = b.EndIndicator.TGrid.TotalGrid },
                    _ => null,
                },
                endXGrid = obj switch
                {
                    IndividualSoflanArea a => new { unit = a.EndIndicator.XGrid.Unit, grid = a.EndIndicator.XGrid.Grid, totalGrid = a.EndIndicator.XGrid.TotalGrid },
                    _ => null,
                },
                soflanType = obj switch
                {
                    KeyframeSoflan => "keyframe",
                    InterpolatableSoflan => "interpolatable",
                    ISoflan => "duration",
                    _ => default,
                },
                soflanSpeed = (obj as ISoflan)?.Speed,
                soflanGroup = obj switch
                {
                    ISoflan soflan => soflan.SoflanGroup,
                    IndividualSoflanArea areaOwner => areaOwner.SoflanGroup,
                    _ => (int?)null,
                },
                applySpeedInDesignMode = (obj as ISoflan)?.ApplySpeedInDesignMode,
            };
        }
    }
}
