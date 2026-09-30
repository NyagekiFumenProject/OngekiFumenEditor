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
                    return RangeOf(fumen.MeterChanges, min, max);
                case "hold":
                    return fumen.Holds.Where(x => InRange(x, min, max));
                case "lane":
                    return fumen.Lanes.Where(x => InRange(x, min, max));
                case "soflan":
                    return fumen.SoflansMap.Values.SelectMany(x => x).OfType<OngekiObjectBase>().Where(x => InRange(x, min, max));
                default:
                    return default;
            }
        }

        private static IEnumerable<OngekiObjectBase> RangeOf<T>(IBinaryFindRangeEnumable<T, TGrid> list, TGrid min, TGrid max)
            where T : OngekiObjectBase, ITimelineObject
            => list.BinaryFindRange(min, max).Cast<OngekiObjectBase>();

        private static bool InRange(OngekiObjectBase obj, TGrid min, TGrid max)
            => obj is ITimelineObject timeline && min <= timeline.TGrid && timeline.TGrid <= max;

        private static object ToObjectDto(string family, OngekiObjectBase obj)
        {
            var tGrid = (obj as ITimelineObject)?.TGrid;
            var xGrid = (obj as OngekiMovableObjectBase)?.XGrid;

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
                enemyWave = obj is EnemySet enemy ? enemy.TagTblValue.ToString() : default,
                laneType = obj is LaneStartBase lane ? lane.LaneType.ToString() : default,
                // lane 的 RecordId 就是 referenceLaneRecordId 的取值来源（§58）。
                recordId = obj is LaneStartBase laneStart ? laneStart.RecordId : (int?)null,
                hasHoldEnd = obj is Hold holdObj ? holdObj.HoldEnd is not null : (bool?)null,
                endTGrid = obj switch
                {
                    Hold h when h.HoldEnd is { } he => new { unit = he.TGrid.Unit, grid = he.TGrid.Grid, totalGrid = he.TGrid.TotalGrid },
                    KeyframeSoflan => null,
                    ISoflan s => new { unit = s.EndTGrid.Unit, grid = s.EndTGrid.Grid, totalGrid = s.EndTGrid.TotalGrid },
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
                soflanGroup = (obj as ISoflan)?.SoflanGroup,
                applySpeedInDesignMode = (obj as ISoflan)?.ApplySpeedInDesignMode,
            };
        }
    }
}
