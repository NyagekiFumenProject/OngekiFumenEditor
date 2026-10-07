using Gemini.Modules.UndoRedo;
using Gemini.Modules.UndoRedo.UndoAction;
using ModelContextProtocol.Server;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.EditorObjects;
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
        private static IBulletPalleteReferencable RequirePalleteReferencable(OngekiObjectBase obj)
            => obj as IBulletPalleteReferencable ?? throw new ArgumentException($"Object #{obj.Id} ({obj.GetType().Name}) does not reference a bullet pallete.");

        private static MeterChange RequireMeter(OngekiObjectBase obj)
            => obj as MeterChange ?? throw new ArgumentException($"Object #{obj.Id} ({obj.GetType().Name}) is not a meter change.");

        private static EnemySet RequireEnemySet(OngekiObjectBase obj)
            => obj as EnemySet ?? throw new ArgumentException($"Object #{obj.Id} ({obj.GetType().Name}) is not an enemy set.");

        private static ISoflan RequireSoflan(OngekiObjectBase obj)
            => obj as ISoflan ?? throw new ArgumentException($"Object #{obj.Id} ({obj.GetType().Name}) is not a soflan.");

        private static TGrid RequireEndTGrid(OngekiObjectBase obj) => obj switch
        {
            Hold hold => hold.HoldEnd?.TGrid ?? throw new ArgumentException($"Hold #{hold.Id} has no end; create one with editor.create_hold_end."),
            KeyframeSoflan => throw new ArgumentException($"Object #{obj.Id} is a keyframe soflan: it is a single point and has no end."),
            ISoflan soflan => soflan.EndTGrid,
            IndividualSoflanArea area => area.EndIndicator.TGrid,
            LaneBlockArea block => block.EndIndicator.TGrid,
            _ => throw new ArgumentException($"Object #{obj.Id} ({obj.GetType().Name}) has no end position."),
        };

        private static void WriteEndTGrid(OngekiObjectBase obj, string propertyName, string rawValue)
        {
            static TGrid Combine(string propertyName, string rawValue, TGrid current)
                => propertyName == "endTGridUnit"
                    ? new TGrid(ParseFloat(rawValue), current.Grid)
                    : new TGrid(current.Unit, ParseInt(rawValue));

            switch (obj)
            {
                case Hold hold:
                    {
                        var end = hold.HoldEnd ?? throw new ArgumentException($"Hold #{hold.Id} has no end; create one with editor.create_hold_end.");
                        end.TGrid = Combine(propertyName, rawValue, end.TGrid);
                        return;
                    }
                case KeyframeSoflan:
                    throw new ArgumentException($"Object #{obj.Id} is a keyframe soflan: it is a single point and has no end.");
                case ISoflan soflan:
                    soflan.EndTGrid = Combine(propertyName, rawValue, soflan.EndTGrid);
                    return;
                case IndividualSoflanArea area:
                    area.EndIndicator.TGrid = Combine(propertyName, rawValue, area.EndIndicator.TGrid);
                    return;
                case LaneBlockArea block:
                    block.EndIndicator.TGrid = Combine(propertyName, rawValue, block.EndIndicator.TGrid);
                    return;
                default:
                    throw new ArgumentException($"Object #{obj.Id} ({obj.GetType().Name}) has no end position.");
            }
        }

        private static int ParsePositiveInt(string rawValue, string propertyName)
        {
            var value = ParseInt(rawValue);
            if (value <= 0)
                throw new ArgumentException($"'{rawValue}' must be a positive number for {propertyName}.");
            return value;
        }

        private static float ParseFiniteFloat(string rawValue, string propertyName)
        {
            var value = ParseFloat(rawValue);
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException($"'{rawValue}' must be a finite number for {propertyName}.");
            return value;
        }

        private static TEnum ParsePalleteEnum<TEnum>(string raw, string propertyName) where TEnum : struct, Enum
        {
            if (Enum.TryParse<TEnum>(raw?.Trim(), true, out var value) && Enum.IsDefined(value))
                return value;
            throw new ArgumentException($"'{raw}' is not a valid {propertyName}; expected one of {string.Join(", ", Enum.GetNames<TEnum>())}.");
        }

        private static string ReadPalleteProperty(BulletPallete pallete, string propertyName)
        {
            switch (propertyName)
            {
                case "editorName":
                    return pallete.EditorName ?? string.Empty;
                case "shooter":
                    return pallete.ShooterValue.ToString();
                case "target":
                    return pallete.TargetValue.ToString();
                case "size":
                    return pallete.SizeValue.ToString();
                case "type":
                    return pallete.TypeValue.ToString();
                case "speed":
                    return pallete.Speed.ToString(CultureInfo.InvariantCulture);
                case "placeOffset":
                    return pallete.PlaceOffset.ToString(CultureInfo.InvariantCulture);
                case "randomOffsetRange":
                    return pallete.RandomOffsetRange.ToString(CultureInfo.InvariantCulture);
                default:
                    throw new ArgumentException($"Unsupported bullet pallete property '{propertyName}'.");
            }
        }

        private static void WritePalleteProperty(BulletPallete pallete, string propertyName, string rawValue)
        {
            switch (propertyName)
            {
                case "editorName":
                    pallete.EditorName = rawValue ?? string.Empty;
                    return;
                case "shooter":
                    pallete.ShooterValue = ParsePalleteEnum<Shooter>(rawValue, propertyName);
                    return;
                case "target":
                    pallete.TargetValue = ParsePalleteEnum<Target>(rawValue, propertyName);
                    return;
                case "size":
                    pallete.SizeValue = ParsePalleteEnum<BulletSize>(rawValue, propertyName);
                    return;
                case "type":
                    pallete.TypeValue = ParsePalleteEnum<BulletType>(rawValue, propertyName);
                    return;
                case "speed":
                    pallete.Speed = ParseFloat(rawValue);
                    return;
                case "placeOffset":
                    pallete.PlaceOffset = ParseInt(rawValue);
                    return;
                case "randomOffsetRange":
                    pallete.RandomOffsetRange = ParseInt(rawValue);
                    return;
                default:
                    throw new ArgumentException($"Unsupported bullet pallete property '{propertyName}'.");
            }
        }

        private static Flick.FlickDirection ParseDirection(string direction)
        {
            return (direction ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "left" => Flick.FlickDirection.Left,
                "right" => Flick.FlickDirection.Right,
                _ => throw new ArgumentException("The flick family requires direction 'left' or 'right'."),
            };
        }

        private static string ReadProperty(OngekiObjectBase obj, string propertyName)
        {
            switch (propertyName)
            {
                case "tGridUnit":
                    return RequireTimeline(obj).TGrid.Unit.ToString(CultureInfo.InvariantCulture);
                case "tGridGrid":
                    return RequireTimeline(obj).TGrid.Grid.ToString(CultureInfo.InvariantCulture);
                case "xGridUnit":
                    return RequireMovable(obj).XGrid.Unit.ToString(CultureInfo.InvariantCulture);
                case "xGridGrid":
                    return RequireMovable(obj).XGrid.Grid.ToString(CultureInfo.InvariantCulture);
                case "isCritical":
                    return RequireCritical(obj).IsCritical.ToString(CultureInfo.InvariantCulture);
                case "direction":
                    return RequireFlick(obj).Direction.ToString();
                case "content":
                    return RequireComment(obj).Content ?? string.Empty;
                case "bpm":
                    return RequireBpm(obj).BPM.ToString(CultureInfo.InvariantCulture);
                case "bulletPallete":
                    return RequirePalleteReferencable(obj).ReferenceBulletPallete?.StrID ?? string.Empty;
                case "shooter":
                case "target":
                case "size":
                case "type":
                case "placeOffset":
                case "randomOffsetRange":
                    RequireProjectilePropertyApplicable(obj, propertyName);
                    return ReadProjectileProperty(RequireProjectile(obj), propertyName);
                case "bulletDamageType":
                    RequireProjectilePropertyApplicable(obj, propertyName);
                    return obj is Bullet bulletWithDamageType
                        ? bulletWithDamageType.BulletDamageTypeValue.ToString()
                        : throw new ArgumentException($"Object #{obj.Id} ({obj.GetType().Name}) is not a bullet.");
                case "bunShi":
                    return RequireMeter(obj).BunShi.ToString(CultureInfo.InvariantCulture);
                case "bunbo":
                    return RequireMeter(obj).Bunbo.ToString(CultureInfo.InvariantCulture);
                case "enemyWave":
                    return RequireEnemySet(obj).TagTblValue.ToString();
                case "endTGridUnit":
                    return RequireEndTGrid(obj).Unit.ToString(CultureInfo.InvariantCulture);
                case "endTGridGrid":
                    return RequireEndTGrid(obj).Grid.ToString(CultureInfo.InvariantCulture);
                case "speed":
                    switch (obj)
                    {
                        case ISoflan soflan:
                            return soflan.Speed.ToString(CultureInfo.InvariantCulture);
                        case IProjectile projectile:
                            return projectile.Speed.ToString(CultureInfo.InvariantCulture);
                        default:
                            throw new ArgumentException($"Object #{obj.Id} ({obj.GetType().Name}) has no speed.");
                    }
                case "soflanGroup":
                    return obj switch
                    {
                        ISoflan soflan => soflan.SoflanGroup.ToString(CultureInfo.InvariantCulture),
                        IndividualSoflanArea area => area.SoflanGroup.ToString(CultureInfo.InvariantCulture),
                        _ => throw new ArgumentException($"Object #{obj.Id} ({obj.GetType().Name}) has no soflanGroup."),
                    };
                case "applySpeedInDesignMode":
                    return RequireSoflan(obj).ApplySpeedInDesignMode.ToString(CultureInfo.InvariantCulture);
                case "widthId":
                    return RequireBeam(obj).WidthId.Id.ToString(CultureInfo.InvariantCulture);
                case "obliqueSourceXGridUnit":
                    return RequireBeam(obj).ObliqueSourceXGridOffset?.Unit.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                case "obliqueSourceXGridGrid":
                    return RequireBeam(obj).ObliqueSourceXGridOffset?.Grid.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                case "colorId":
                    return RequireColorful(obj).ColorId.Id.ToString(CultureInfo.InvariantCulture);
                case "brightness":
                    return RequireColorful(obj).Brightness.ToString(CultureInfo.InvariantCulture);
                case "endXGridUnit":
                    return RequireSoflanArea(obj).EndIndicator.XGrid.Unit.ToString(CultureInfo.InvariantCulture);
                case "endXGridGrid":
                    return RequireSoflanArea(obj).EndIndicator.XGrid.Grid.ToString(CultureInfo.InvariantCulture);
                case "blockDirection":
                    return RequireLaneBlock(obj).Direction.ToString();
                case ReferenceLaneRecordIdProperty:
                    return (obj as ILaneDockable)?.ReferenceLaneStrId is int laneId && laneId >= 0
                        ? laneId.ToString(CultureInfo.InvariantCulture)
                        : string.Empty;
                default:
                    throw new ArgumentException($"Unsupported property '{propertyName}'.");
            }
        }

        private static void WriteProperty(OngekiObjectBase obj, string propertyName, string rawValue, OngekiFumen fumen)
        {
            switch (propertyName)
            {
                case "tGridUnit":
                {
                    var timeline = RequireTimeline(obj);
                    timeline.TGrid = new TGrid(ParseFloat(rawValue), timeline.TGrid.Grid);
                    ReorderConnectableChild(obj);
                    return;
                }
                case "tGridGrid":
                {
                    var timeline = RequireTimeline(obj);
                    timeline.TGrid = new TGrid(timeline.TGrid.Unit, ParseInt(rawValue));
                    ReorderConnectableChild(obj);
                    return;
                }
                case "xGridUnit":
                {
                    var movable = RequireMovable(obj);
                    movable.XGrid = new XGrid(ParseFloat(rawValue), movable.XGrid.Grid);
                    return;
                }
                case "xGridGrid":
                {
                    var movable = RequireMovable(obj);
                    movable.XGrid = new XGrid(movable.XGrid.Unit, ParseInt(rawValue));
                    return;
                }
                case "isCritical":
                    RequireCritical(obj).IsCritical = ParseBool(rawValue);
                    return;
                case "direction":
                    RequireFlick(obj).Direction = ParseDirection(rawValue);
                    return;
                case "content":
                    RequireComment(obj).Content = rawValue ?? string.Empty;
                    return;
                case "bpm":
                    RequireBpm(obj).BPM = ParseDouble(rawValue);
                    return;
                case "bulletPallete":
                {
                    var referencable = RequirePalleteReferencable(obj);
                    var requested = rawValue?.Trim();
                    if (string.IsNullOrEmpty(requested))
                    {
                        // §42：清空即回到 custom 模式。bullet 从 add_object 支持自定义参数起也允许滞空，
                        // 否则 bullet 一旦绑过 palette 就再也回不到 custom。
                        referencable.ReferenceBulletPallete = default;
                        return;
                    }

                    if (requested == Bell.OngekiDefaultBellPaletteName)
                    {
                        if (obj is not Bell)
                            throw new ArgumentException($"'{Bell.OngekiDefaultBellPaletteName}' marks the Ongeki default bell and cannot be used for a bullet; clear the pallete with an empty value to switch a bullet to custom parameters.");
                        referencable.ReferenceBulletPallete = default;
                        return;
                    }

                    // 与 add 侧一致：精确查找，避免 BulletPalleteList 索引器把未知字符串折算成数字 id 撞上别的调色板。
                    referencable.ReferenceBulletPallete = LookupBulletPallete(fumen, requested)
                        ?? throw new ArgumentException($"No bullet pallete '{requested}' in the editor.");
                    return;
                }
                case "bunShi":
                    RequireMeter(obj).BunShi = ParsePositiveInt(rawValue, propertyName);
                    return;
                case "bunbo":
                    RequireMeter(obj).Bunbo = ParsePositiveInt(rawValue, propertyName);
                    return;
                case "enemyWave":
                    RequireEnemySet(obj).TagTblValue = ParsePalleteEnum<EnemySet.WaveChangeConst>(rawValue, propertyName);
                    return;
                case "endTGridUnit":
                case "endTGridGrid":
                    WriteEndTGrid(obj, propertyName, rawValue);
                    return;
                case "speed":
                    if (obj is ISoflan soflan)
                    {
                        soflan.Speed = ParseFiniteFloat(rawValue, propertyName);
                        return;
                    }
                    WriteProjectileProperty(obj, propertyName, rawValue);
                    return;
                case "shooter":
                case "target":
                case "size":
                case "type":
                case "placeOffset":
                case "randomOffsetRange":
                case "bulletDamageType":
                    WriteProjectileProperty(obj, propertyName, rawValue);
                    return;
                case "soflanGroup":
                    if (obj is IndividualSoflanArea soflanArea)
                    {
                        // IndividualSoflanAreaMap 是按 SoflanGroup 分桶的字典 + 区间树，
                        // 改组必须重新入桶，否则对象会留在旧组的列表里、新组查不到。
                        fumen.IndividualSoflanAreaMap.Remove(soflanArea);
                        soflanArea.SoflanGroup = ParseInt(rawValue);
                        fumen.IndividualSoflanAreaMap.Add(soflanArea);
                        return;
                    }
                    RequireSoflan(obj).SoflanGroup = ParseInt(rawValue);
                    return;
                case "applySpeedInDesignMode":
                    RequireSoflan(obj).ApplySpeedInDesignMode = ParseBool(rawValue);
                    return;
                case "widthId":
                    RequireBeam(obj).WidthId = ParseWidthId(rawValue);
                    return;
                case "obliqueSourceXGridUnit":
                case "obliqueSourceXGridGrid":
                    {
                        var beam = RequireBeam(obj);
                        beam.ObliqueSourceXGridOffset = WriteXGridComponent(beam.ObliqueSourceXGridOffset, propertyName, rawValue);
                        return;
                    }
                case "colorId":
                    RequireColorful(obj).ColorId = ParseColorId(rawValue);
                    return;
                case "brightness":
                    RequireColorful(obj).Brightness = ParseInt(rawValue);
                    return;
                case "endXGridUnit":
                case "endXGridGrid":
                    {
                        var area = RequireSoflanArea(obj);
                        area.EndIndicator.XGrid = WriteXGridComponent(area.EndIndicator.XGrid, propertyName, rawValue);
                        return;
                    }
                case "blockDirection":
                    RequireLaneBlock(obj).Direction = ParseLaneBlockDirection(rawValue);
                    return;
                case ReferenceLaneRecordIdProperty:
                {
                    // §58：数字绑定；空串 / null / 负数（UI 的 -1 哨兵）滞空。
                    var dockable = RequireLaneDockable(obj);
                    dockable.ReferenceLaneStart = TryParseLaneRecordId(rawValue, out var recordId)
                        ? ResolveLaneByRecordId(fumen, recordId)
                        : default;
                    return;
                }
                default:
                    throw new ArgumentException($"Unsupported property '{propertyName}'.");
            }
        }

        private static IProjectile RequireProjectile(OngekiObjectBase obj)
            => obj as IProjectile ?? throw new ArgumentException($"Object #{obj.Id} ({obj.GetType().Name}) has no projectile parameters.");

        /// <summary>
        /// Bell 的 SizeValue 无效果、TypeValue 恒为 Circle，BulletDamageTypeValue 只属于 Bullet ——
        /// 与 add 侧同一套家族限制，保证两侧报错一致。
        /// </summary>
        private static void RequireProjectilePropertyApplicable(OngekiObjectBase obj, string propertyName)
        {
            if (obj is not Bell)
                return;

            switch (propertyName)
            {
                case "size":
                    throw new ArgumentException("'size' has no effect on bells.");
                case "type":
                    throw new ArgumentException("'type' cannot be set on a bell: a bell is always a Circle.");
                case "bulletDamageType":
                    throw new ArgumentException("'bulletDamageType' only applies to bullets.");
            }
        }

        private static string ReadProjectileProperty(IProjectile projectile, string propertyName)
        {
            return propertyName switch
            {
                "shooter" => projectile.ShooterValue.ToString(),
                "target" => projectile.TargetValue.ToString(),
                "size" => projectile.SizeValue.ToString(),
                "type" => projectile.TypeValue.ToString(),
                "placeOffset" => projectile.PlaceOffset.ToString(CultureInfo.InvariantCulture),
                "randomOffsetRange" => projectile.RandomOffsetRange.ToString(CultureInfo.InvariantCulture),
                "speed" => projectile.Speed.ToString(CultureInfo.InvariantCulture),
                _ => throw new ArgumentException($"Unsupported projectile property '{propertyName}'."),
            };
        }

        /// <summary>
        /// 写 custom projectile 参数。「palette 非空时只读」的请求级校验在 modify_object 的入口完成；
        /// 这里不重复拦截 —— undo/redo 的回放可能在对象已经重新绑上 palette 的时刻恢复旧本地值，
        /// 该值对 palette 不可见，但清掉 palette 后就该看到它。
        /// </summary>
        private static void WriteProjectileProperty(OngekiObjectBase obj, string propertyName, string rawValue)
        {
            switch (obj)
            {
                case Bell bell:
                    switch (propertyName)
                    {
                        case "shooter":
                            bell.ShooterValue = ParsePalleteEnum<Shooter>(rawValue, propertyName);
                            return;
                        case "target":
                            bell.TargetValue = ParsePalleteEnum<Target>(rawValue, propertyName);
                            return;
                        case "speed":
                            bell.Speed = ParseFiniteFloat(rawValue, propertyName);
                            return;
                        case "placeOffset":
                            bell.PlaceOffset = ParseInt(rawValue);
                            return;
                        case "randomOffsetRange":
                            bell.RandomOffsetRange = ParseInt(rawValue);
                            return;
                    }
                    break;

                case Bullet bullet:
                    switch (propertyName)
                    {
                        case "shooter":
                            bullet.ShooterValue = ParsePalleteEnum<Shooter>(rawValue, propertyName);
                            return;
                        case "target":
                            bullet.TargetValue = ParsePalleteEnum<Target>(rawValue, propertyName);
                            return;
                        case "speed":
                            bullet.Speed = ParseFiniteFloat(rawValue, propertyName);
                            return;
                        case "placeOffset":
                            bullet.PlaceOffset = ParseInt(rawValue);
                            return;
                        case "randomOffsetRange":
                            bullet.RandomOffsetRange = ParseInt(rawValue);
                            return;
                        case "size":
                            bullet.SizeValue = ParsePalleteEnum<BulletSize>(rawValue, propertyName);
                            return;
                        case "type":
                            bullet.TypeValue = ParsePalleteEnum<BulletType>(rawValue, propertyName);
                            return;
                        case "bulletDamageType":
                            bullet.BulletDamageTypeValue = ParsePalleteEnum<BulletDamageType>(rawValue, propertyName);
                            return;
                    }
                    break;
            }

            throw new ArgumentException($"Object #{obj.Id} ({obj.GetType().Name}) has no writable projectile property '{propertyName}'.");
        }

        private static float ParseFloat(string rawValue)
            => float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new ArgumentException($"'{rawValue}' is not a valid number.");

        private static double ParseDouble(string rawValue)
            => double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new ArgumentException($"'{rawValue}' is not a valid number.");

        private static int ParseInt(string rawValue)
            => int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new ArgumentException($"'{rawValue}' is not a valid integer.");

        private static bool ParseBool(string rawValue)
            => bool.TryParse(rawValue, out var value)
                ? value
                : throw new ArgumentException($"'{rawValue}' is not a valid boolean.");

        private static OngekiTimelineObjectBase RequireTimeline(OngekiObjectBase obj)
            => obj as OngekiTimelineObjectBase ?? throw new ArgumentException($"Object #{obj.Id} has no TGrid.");

        private static OngekiMovableObjectBase RequireMovable(OngekiObjectBase obj)
            => obj as OngekiMovableObjectBase ?? throw new ArgumentException($"Object #{obj.Id} has no XGrid.");

        private static ICriticalableObject RequireCritical(OngekiObjectBase obj)
            => obj as ICriticalableObject ?? throw new ArgumentException($"Object #{obj.Id} has no isCritical property.");

        private static Flick RequireFlick(OngekiObjectBase obj)
            => obj as Flick ?? throw new ArgumentException($"Object #{obj.Id} is not a flick.");

        private static Comment RequireComment(OngekiObjectBase obj)
            => obj as Comment ?? throw new ArgumentException($"Object #{obj.Id} is not a comment.");

        private static BPMChange RequireBpm(OngekiObjectBase obj)
            => obj as BPMChange ?? throw new ArgumentException($"Object #{obj.Id} is not a bpm change.");

        private static IBeamObject RequireBeam(OngekiObjectBase obj)
            => obj as IBeamObject ?? throw new ArgumentException($"Object #{obj.Id} ({obj.GetType().Name}) is not a beam.");

        private static IColorfulLane RequireColorful(OngekiObjectBase obj)
            => obj as IColorfulLane ?? throw new ArgumentException($"Object #{obj.Id} ({obj.GetType().Name}) is not a colorful lane.");

        private static IndividualSoflanArea RequireSoflanArea(OngekiObjectBase obj)
            => obj as IndividualSoflanArea ?? throw new ArgumentException($"Object #{obj.Id} ({obj.GetType().Name}) is not an individual soflan area.");

        private static LaneBlockArea RequireLaneBlock(OngekiObjectBase obj)
            => obj as LaneBlockArea ?? throw new ArgumentException($"Object #{obj.Id} ({obj.GetType().Name}) is not a lane block area.");

        /// <summary>
        /// 写 XGrid 的单个分量。空串表示「清空」（目前只有光束的斜光束源偏移允许为空）。
        /// </summary>
        private static XGrid WriteXGridComponent(XGrid current, string propertyName, string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return default;

            var basis = current ?? new XGrid(0, 0);
            return propertyName.EndsWith("Unit", StringComparison.Ordinal)
                ? new XGrid(ParseFloat(rawValue), basis.Grid)
                : new XGrid(basis.Unit, ParseInt(rawValue));
        }

        /// <summary>
        /// 光束宽度。取值域是 <see cref="WidthIdConst"/> 的 1..5；越界直接报错而不是落到默认宽度 ——
        /// <see cref="WidthId.ParseFromId(int)"/> 对未知 id 会静默退化成 Id_1，写错值会被当成合法设置。
        /// </summary>
        private static WidthId ParseWidthId(string rawValue)
        {
            var id = ParseInt(rawValue);
            var found = WidthIdConst.AllWidthIds.FirstOrDefault(x => x.Id == id);
            if (found is null)
                throw new ArgumentException($"'{rawValue}' is not a valid widthId; valid ids are {string.Join(", ", WidthIdConst.AllWidthIds.Select(x => x.Id))}.");
            return found;
        }

        /// <summary>色带 lane 的颜色：接受 <see cref="ColorIdConst.AllColors"/> 里的名字（忽略大小写）或数字 Id。</summary>
        private static ColorId ParseColorId(string rawValue)
        {
            var text = (rawValue ?? string.Empty).Trim();
            if (text.Length == 0)
                throw new ArgumentException("colorId requires a colour name or numeric id.");

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                var byId = ColorIdConst.AllColors.FirstOrDefault(x => x.Id == id);
                if (byId.Name is not null)
                    return byId;
                throw new ArgumentException($"'{rawValue}' is not a valid colorId; valid ids are {string.Join(", ", ColorIdConst.AllColors.Select(x => x.Id))}.");
            }

            var byName = ColorIdConst.AllColors.FirstOrDefault(x => string.Equals(x.Name, text, StringComparison.OrdinalIgnoreCase));
            if (byName.Name is not null)
                return byName;

            throw new ArgumentException($"'{rawValue}' is not a valid colorId; valid names are {string.Join(", ", ColorIdConst.AllColors.Select(x => x.Name))}.");
        }
    }
}
