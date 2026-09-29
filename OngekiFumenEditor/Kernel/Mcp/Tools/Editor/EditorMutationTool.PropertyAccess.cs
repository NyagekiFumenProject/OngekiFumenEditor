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
            _ => throw new ArgumentException($"Object #{obj.Id} ({obj.GetType().Name}) has no end position."),
        };

        private static void WriteEndTGrid(OngekiObjectBase obj, string propertyName, string rawValue)
        {
            switch (obj)
            {
                case Hold hold:
                    {
                        var end = hold.HoldEnd ?? throw new ArgumentException($"Hold #{hold.Id} has no end; create one with editor.create_hold_end.");
                        end.TGrid = propertyName == "endTGridUnit"
                            ? new TGrid(ParseFloat(rawValue), end.TGrid.Grid)
                            : new TGrid(end.TGrid.Unit, ParseInt(rawValue));
                        return;
                    }
                case KeyframeSoflan:
                    throw new ArgumentException($"Object #{obj.Id} is a keyframe soflan: it is a single point and has no end.");
                case ISoflan soflan:
                    soflan.EndTGrid = propertyName == "endTGridUnit"
                        ? new TGrid(ParseFloat(rawValue), soflan.EndTGrid.Grid)
                        : new TGrid(soflan.EndTGrid.Unit, ParseInt(rawValue));
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
                    return RequireSoflan(obj).Speed.ToString(CultureInfo.InvariantCulture);
                case "soflanGroup":
                    return RequireSoflan(obj).SoflanGroup.ToString(CultureInfo.InvariantCulture);
                case "applySpeedInDesignMode":
                    return RequireSoflan(obj).ApplySpeedInDesignMode.ToString(CultureInfo.InvariantCulture);
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
                    return;
                }
                case "tGridGrid":
                {
                    var timeline = RequireTimeline(obj);
                    timeline.TGrid = new TGrid(timeline.TGrid.Unit, ParseInt(rawValue));
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
                    if (string.IsNullOrEmpty(requested) || requested == Bell.OngekiDefaultBellPaletteName)
                    {
                        if (obj is not Bell)
                            throw new ArgumentException("A bullet must reference a bullet pallete; only bells can fall back to the Ongeki default bell.");
                        referencable.ReferenceBulletPallete = default;
                        return;
                    }

                    referencable.ReferenceBulletPallete = fumen.BulletPalleteList[requested]
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
                    RequireSoflan(obj).Speed = ParseFiniteFloat(rawValue, propertyName);
                    return;
                case "soflanGroup":
                    RequireSoflan(obj).SoflanGroup = ParseInt(rawValue);
                    return;
                case "applySpeedInDesignMode":
                    RequireSoflan(obj).ApplySpeedInDesignMode = ParseBool(rawValue);
                    return;
                default:
                    throw new ArgumentException($"Unsupported property '{propertyName}'.");
            }
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
    }
}
