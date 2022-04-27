using Caliburn.Micro;
using DereTore.Common;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.DefaultImpl
{
    [Export(typeof(IFumenSerializable))]
    class DefaultNyagekiFumenFormatter : IFumenSerializable
    {
        public string FileFormatName => DefaultNyagekiFumenParser.FormatName;
        public string[] SupportFumenFileExtensions => DefaultNyagekiFumenParser.FumenFileExtensions;

        public static JsonSerializerOptions DefaultJsonOption { get; } = new JsonSerializerOptions()
        {
            WriteIndented = true,
            IgnoreReadOnlyFields = true,
            IncludeFields = false,
            IgnoreReadOnlyProperties = true
        };

        static DefaultNyagekiFumenFormatter()
        {
            foreach (var converter in IoC.GetAll<JsonConverter>())
                DefaultJsonOption.Converters.Add(converter);
        }

        public async Task<byte[]> SerializeAsync(OngekiFumen fumen)
        {
            using var memory = new MemoryStream();
            using var writer = new StreamWriter(memory);

            ProcessHEADER(fumen, writer);

            ProcessB_PALETTE(fumen, writer);

            ProcessCOMPOSITION(fumen, writer);

            ProcessLANE(fumen, writer);

            ProcessBEAM(fumen, writer);

            ProcessBELL(fumen, writer);

            ProcessBULLET(fumen, writer);

            ProcessLANE_BLOCK(fumen, writer);

            ProcessFLICK(fumen, writer);

            ProcessNOTES(fumen, writer);

            ProcessCURVE(fumen, writer);

            await writer.FlushAsync();
            await memory.FlushAsync();

            return memory.ToArray();
        }

        private void ProcessCURVE(OngekiFumen fumen, StreamWriter writer)
        {
            var childObjects = fumen.Lanes
                .AsEnumerable<ConnectableStartObject>()
                .Concat(fumen.Beams)
                .SelectMany(x => x.Children)
                .Where(x => x.IsCurvePath).ToList();
            foreach (var child in childObjects)
            {
                writer.Write("CurveControlPoint:");
                writer.Write(child.ReferenceStartObject.RecordId);
                writer.Write(":");
                writer.Write(child.ReferenceStartObject.IDShortName);
                writer.Write(":");
                writer.Write(child.ReferenceStartObject.Children.FirstIndexOf(x => x == child));
                writer.Write(":");
                writer.Write(child.CurvePrecision);
                writer.Write(":");
                writer.Write(string.Join(" -> ", child.PathControls.Select(x => $"(X[{x.XGrid.Unit},{x.XGrid.Grid}],X[{x.TGrid.Unit},{x.TGrid.Grid}])")));
                writer.WriteLine();
            }
            writer.WriteLine();
        }

        private void ProcessLANE_BLOCK(OngekiFumen fumen, StreamWriter writer)
        {
            foreach (var blk in fumen.LaneBlocks.OrderBy(x => x.TGrid))
                writer.WriteLine($"LaneBlock:{blk.Direction}:(T[{blk.TGrid.Unit},{blk.TGrid.Grid}]) -> (T[{blk.EndIndicator.TGrid.Unit},{blk.EndIndicator.TGrid.Grid}])");
            writer.WriteLine();
        }

        public void ProcessHEADER(OngekiFumen fumen, StreamWriter sb)
        {
            var metaInfo = fumen.MetaInfo;

            sb.WriteLine($"Header:{nameof(metaInfo.Version)}:{metaInfo.Version}");
            sb.WriteLine($"Header:{nameof(metaInfo.Creator)}:{metaInfo.Creator}");
            sb.WriteLine($"Header:{nameof(metaInfo.BpmDefinition.First)}Bpm:{metaInfo.BpmDefinition.First}");
            sb.WriteLine($"Header:{nameof(metaInfo.BpmDefinition.Common)}Bpm:{metaInfo.BpmDefinition.Common}");
            sb.WriteLine($"Header:{nameof(metaInfo.BpmDefinition.Maximum)}Bpm:{metaInfo.BpmDefinition.Maximum}");
            sb.WriteLine($"Header:{nameof(metaInfo.BpmDefinition.Minimum)}Bpm:{metaInfo.BpmDefinition.Minimum}");
            sb.WriteLine($"Header:Meter:{metaInfo.MeterDefinition.Bunshi}/{metaInfo.MeterDefinition.Bunbo}");
            sb.WriteLine($"Header:{nameof(metaInfo.TRESOLUTION)}:{metaInfo.TRESOLUTION}");
            sb.WriteLine($"Header:{nameof(metaInfo.XRESOLUTION)}:{metaInfo.XRESOLUTION}");
            sb.WriteLine($"Header:{nameof(metaInfo.ClickDefinition)}:{metaInfo.ClickDefinition}");
            sb.WriteLine($"Header:{nameof(metaInfo.Tutorial)}:{metaInfo.Tutorial}");
            sb.WriteLine($"Header:{nameof(metaInfo.BeamDamage)}:{metaInfo.BeamDamage}");
            sb.WriteLine($"Header:{nameof(metaInfo.HardBulletDamage)}:{metaInfo.HardBulletDamage}");
            sb.WriteLine($"Header:{nameof(metaInfo.DangerBulletDamage)}:{metaInfo.DangerBulletDamage}");
            sb.WriteLine($"Header:{nameof(metaInfo.BulletDamage)}:{metaInfo.BulletDamage}");
            sb.WriteLine($"Header:{nameof(metaInfo.ProgJudgeBpm)}:{metaInfo.ProgJudgeBpm}");
            
            sb.WriteLine();
        }

        public void ProcessB_PALETTE(OngekiFumen fumen, StreamWriter sb)
        {
            foreach (var bpl in fumen.BulletPalleteList.OrderBy(x => x.StrID))
            {
                sb.Write($"BulletPallete:");
                sb.Write(bpl.StrID);
                sb.Write(":");
                sb.Write($"Target[{bpl.TargetValue}]");
                sb.Write(",");
                sb.Write($"Size[{bpl.SizeValue}]");
                sb.Write(",");
                sb.Write($"Type[{bpl.TypeValue}]");
                sb.Write(",");
                sb.Write($"Speed[{bpl.Speed}]");
                sb.Write(",");
                sb.Write($"Offset[{bpl.PlaceOffset}]");
                sb.Write(",");
                sb.Write($"Shooter[{bpl.ShooterValue}]");
                sb.WriteLine();
            }
            sb.WriteLine();
        }

        public void ProcessCOMPOSITION(OngekiFumen fumen, StreamWriter sb)
        {
            foreach (var clk in fumen.ClickSEs.OrderBy(x => x.TGrid))
                sb.WriteLine($"ClickSE:T[{clk.TGrid.Unit},{clk.TGrid.Grid}]");
            foreach (var est in fumen.EnemySets.OrderBy(x => x.TGrid))
                sb.WriteLine($"EnemySet:{est.TagTblValue}:T[{est.TGrid.Unit},{est.TGrid.Grid}]");
            foreach (var met in fumen.MeterChanges.OrderBy(x => x.TGrid).Where(x => x.TGrid != fumen.MeterChanges.FirstMeter.TGrid))
                sb.WriteLine($"MeterChange:{met.BunShi}/{met.Bunbo}:T[{met.TGrid.Unit},{met.TGrid.Grid}]");
            sb.WriteLine();
            foreach (var bpm in fumen.BpmList.OrderBy(x => x.TGrid).Where(x => x.TGrid != fumen.BpmList.FirstBpm.TGrid))
                sb.WriteLine($"BpmChange:{bpm.BPM}:T[{bpm.TGrid.Unit},{bpm.TGrid.Grid}]");
            sb.WriteLine();
            foreach (var soflan in fumen.Soflans.OrderBy(x => x.TGrid))
                sb.WriteLine($"Soflan:{soflan.Speed}:(T[{soflan.TGrid.Unit},{soflan.TGrid.Grid}]) -> (T[{soflan.EndTGrid.Unit},{soflan.EndTGrid.Grid}])");
            sb.WriteLine();
        }

        public void ProcessLANE(OngekiFumen fumen, StreamWriter sb)
        {
            var builder = new StringBuilder();

            string Serialize(ConnectableStartObject laneStart)
            {
                builder.Clear();
                builder.Append($"Lane:{laneStart.RecordId}:");

                string SerializeOutput(ConnectableObjectBase o)
                {
                    return $"(Type[{o.IDShortName}],X[{o.XGrid.Unit},{o.XGrid.Grid}],T[{o.TGrid.Unit},{o.TGrid.Grid}]{(o is IColorfulLane c ? $",C[{c.ColorId.Name},{c.Brightness}]" : string.Empty)})";
                }

                var r = string.Join(" -> ", laneStart.Children.AsEnumerable<ConnectableObjectBase>().Prepend(laneStart).Select(x => SerializeOutput(x)));
                builder.Append(r);

                return builder.ToString();
            }

            var groups = fumen.Lanes.GroupBy(x => x.LaneType).OrderByDescending(x => x.Key).ToArray();
            foreach (var group in groups)
            {
                var laneStarts = group.OrderBy(x => x.RecordId).ToArray();
                foreach (var laneStart in laneStarts)
                    sb.WriteLine(Serialize(laneStart));
                sb.WriteLine();
            }
        }

        public void ProcessBULLET(OngekiFumen fumen, StreamWriter sb)
        {
            foreach (var bullet in fumen.Bullets.OrderBy(x => x.TGrid))
                sb.WriteLine($"Bullet:{bullet.ReferenceBulletPallete?.StrID}:X[{bullet.XGrid.Unit},{bullet.XGrid.Grid}],T[{bullet.TGrid.Unit},{bullet.TGrid.Grid}],D[{bullet.BulletDamageTypeValue}]");
            sb.WriteLine();
        }

        public void ProcessBEAM(OngekiFumen fumen, StreamWriter sb)
        {
            var builder = new StringBuilder();

            string Serialize(ConnectableStartObject laneStart)
            {
                builder.Clear();
                builder.Append($"Beam:{laneStart.RecordId}:");

                string SerializeOutput(ConnectableObjectBase o)
                {
                    return $"(Type[{o.IDShortName}],X[{o.XGrid.Unit},{o.XGrid.Grid}],T[{o.TGrid.Unit},{o.TGrid.Grid}],W[{((IBeamObject)o).WidthId}])";
                }

                var r = string.Join(" -> ", laneStart.Children.AsEnumerable<ConnectableObjectBase>().Prepend(laneStart).Select(x => SerializeOutput(x)));
                builder.Append(r);

                return builder.ToString();
            }

            foreach (var beamStart in fumen.Beams.OrderBy(x => x.RecordId))
                sb.WriteLine(Serialize(beamStart));
            sb.WriteLine();
        }

        public void ProcessBELL(OngekiFumen fumen, StreamWriter sb)
        {

            foreach (var bell in fumen.Bells.OrderBy(x => x.TGrid))
                sb.WriteLine($"Bell:{bell.ReferenceBulletPallete?.StrID}:X[{bell.XGrid.Unit},{bell.XGrid.Grid}],T[{bell.TGrid.Unit},{bell.TGrid.Grid}]");
            sb.WriteLine();
        }

        public void ProcessFLICK(OngekiFumen fumen, StreamWriter sb)
        {
            foreach (var flick in fumen.Flicks.OrderBy(x => x.TGrid))
                sb.WriteLine($"Flick:X[{flick.XGrid.Unit},{flick.XGrid.Grid}],T[{flick.TGrid.Unit},{flick.TGrid.Grid}],C[{flick.IsCritical}],D[{flick.Direction}]");
            sb.WriteLine();
        }

        public void ProcessNOTES(OngekiFumen fumen, StreamWriter sb)
        {
            foreach (var tap in fumen.Taps.OrderBy(x => x.TGrid))
                sb.WriteLine($"Tap:{tap.ReferenceLaneStrId}:X[{tap.XGrid.Unit},{tap.XGrid.Grid}],T[{tap.TGrid.Unit},{tap.TGrid.Grid}],C[{tap.IsCritical}]");
            sb.WriteLine();
            foreach (var hold in fumen.Holds.OrderBy(x => x.TGrid))
            {
                sb.Write($"Hold:{hold.ReferenceLaneStrId},{hold.IsCritical}:(X[{hold.XGrid.Unit},{hold.XGrid.Grid}],T[{hold.TGrid.Unit},{hold.TGrid.Grid}])");
                if (hold.HoldEnd is HoldEnd end)
                    sb.Write($" -> (X[{end.XGrid.Unit},{end.XGrid.Grid}],T[{end.TGrid.Unit},{end.TGrid.Grid}])");
                sb.WriteLine();
            }
            sb.WriteLine();
        }
    }
}
