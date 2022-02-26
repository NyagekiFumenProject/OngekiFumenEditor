using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
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
            using var gzip = new GZipStream(memory, CompressionLevel.Optimal);
            using var writer = new BinaryWriter(gzip);

            ProcessHEADER(fumen, writer);

            ProcessB_PALETTE(fumen, writer);

            ProcessCOMPOSITION(fumen, writer);

            ProcessLANE(fumen, writer);

            ProcessBULLET(fumen, writer);

            ProcessBEAM(fumen, writer);

            ProcessBELL(fumen, writer);

            ProcessFLICK(fumen, writer);

            ProcessNOTES(fumen, writer);

            writer.Flush();
            await gzip.FlushAsync();
            gzip.Close();
            await memory.FlushAsync();

            return memory.ToArray();
        }

        public void ProcessHEADER(OngekiFumen fumen, BinaryWriter sb)
        {
            var metaInfo = fumen.MetaInfo;

            sb.Write(metaInfo.Version.Major);
            sb.Write(metaInfo.Version.Minor);
            sb.Write(metaInfo.Version.Build);
            sb.Write(metaInfo.Creator);
            sb.Write(metaInfo.BpmDefinition.First);
            sb.Write(metaInfo.BpmDefinition.Common);
            sb.Write(metaInfo.BpmDefinition.Maximum);
            sb.Write(metaInfo.BpmDefinition.Minimum);
            sb.Write(metaInfo.MeterDefinition.Bunshi);
            sb.Write(metaInfo.MeterDefinition.Bunbo);
            sb.Write(metaInfo.TRESOLUTION);
            sb.Write(metaInfo.XRESOLUTION);
            sb.Write(metaInfo.ClickDefinition);
            sb.Write(metaInfo.ProgJudgeBpm);
            sb.Write(metaInfo.Tutorial);
            sb.Write(metaInfo.BulletDamage);
            sb.Write(metaInfo.HardBulletDamage);
            sb.Write(metaInfo.DangerBulletDamage);
            sb.Write(metaInfo.BeamDamage);
        }

        public void ProcessB_PALETTE(OngekiFumen fumen, BinaryWriter sb)
        {
            sb.Write(fumen.BulletPalleteList.Count());

            foreach (var bpl in fumen.BulletPalleteList.OrderBy(x => x.StrID))
            {
                sb.Write(bpl.StrID);
                sb.Write(bpl.ShooterValue);
                sb.Write(bpl.PlaceOffset);
                sb.Write(bpl.TargetValue);
                sb.Write(bpl.Speed);
                //sb.Write(bpl.BulletTypeValue);
                sb.Write(bpl.SizeValue);
                sb.Write(bpl.TypeValue);
            }
        }


        public void ProcessCOMPOSITION(OngekiFumen fumen, BinaryWriter sb)
        {
            sb.Write(fumen.BpmList.Count() - 1);
            foreach (var o in fumen.BpmList.OrderBy(x => x.TGrid).Where(x => x.TGrid != fumen.BpmList.FirstBpm.TGrid))
            {
                sb.Write(o.TGrid.Unit);
                sb.Write(o.TGrid.Grid);
                sb.Write(o.BPM);
            }

            sb.Write(fumen.MeterChanges.Count() - 1);
            foreach (var o in fumen.MeterChanges.OrderBy(x => x.TGrid).Where(x => x.TGrid != fumen.MeterChanges.FirstMeter.TGrid))
            {
                sb.Write(o.TGrid.Unit);
                sb.Write(o.TGrid.Grid);
                sb.Write(o.BunShi);
                sb.Write(o.Bunbo);
            }

            sb.Write(fumen.ClickSEs.Count);
            foreach (var o in fumen.ClickSEs.OrderBy(x => x.TGrid))
            {
                sb.Write(o.TGrid.Unit);
                sb.Write(o.TGrid.Grid);
            }

            sb.Write(fumen.EnemySets.Count);
            foreach (var o in fumen.EnemySets.OrderBy(x => x.TGrid))
            {
                sb.Write(o.TGrid.Unit);
                sb.Write(o.TGrid.Grid);
                sb.Write(o.TagTblValue);
            }
        }

        public void ProcessLANE(OngekiFumen fumen, BinaryWriter sb)
        {
            void Serialize(ConnectableStartObject laneStart)
            {
                void SerializeOutput(ConnectableObjectBase o)
                {
                    sb.Write(o.IDShortName);
                    sb.Write(o.RecordId);
                    sb.Write(o.TGrid.Unit);
                    sb.Write(o.TGrid.Grid);
                    sb.Write(o.XGrid.Unit);
                }

                sb.Write(laneStart.Children.Count());
                SerializeOutput(laneStart);
                foreach (var child in laneStart.Children)
                    SerializeOutput(child);
            }

            var groups = fumen.Lanes.GroupBy(x => x.LaneType).OrderByDescending(x => x.Key).ToArray();
            sb.Write(groups.Length);
            foreach (var group in groups)
            {
                var laneStarts = group.OrderBy(x => x.RecordId).ToArray();
                sb.Write(laneStarts.Length);
                foreach (var laneStart in laneStarts)
                    Serialize(laneStart);
            }
        }

        public void ProcessBULLET(OngekiFumen fumen, BinaryWriter sb)
        {
            sb.Write(fumen.Bullets.Count);
            foreach (var u in fumen.Bullets.OrderBy(x => x.TGrid))
            {
                sb.Write(u.ReferenceBulletPallete?.StrID);
                sb.Write(u.TGrid.Unit);
                sb.Write(u.TGrid.Grid);
                sb.Write(u.XGrid.Unit);
            }
        }

        public void ProcessBEAM(OngekiFumen fumen, BinaryWriter sb)
        {
            void SerializeOutput(BeamBase o)
            {
                sb.Write(o.IDShortName);
                sb.Write(o.RecordId);
                sb.Write(o.TGrid.Unit);
                sb.Write(o.TGrid.Grid);
                sb.Write(o.XGrid.Unit);
                sb.Write(o.WidthId);
            }

            sb.Write(fumen.Beams.Count());
            foreach (var beamStart in fumen.Beams.OrderBy(x => x.RecordId))
            {
                sb.Write(beamStart.Children.Count() + 1);
                SerializeOutput(beamStart);
                foreach (var child in beamStart.Children)
                    SerializeOutput(child);
            }
        }

        public void ProcessBELL(OngekiFumen fumen, BinaryWriter sb)
        {
            sb.Write(fumen.Bells.Count);
            foreach (var u in fumen.Bells.OrderBy(x => x.TGrid))
            {
                sb.Write(u.TGrid.Unit);
                sb.Write(u.TGrid.Grid);
                sb.Write(u.XGrid.Unit);

                sb.Write(u.ReferenceBulletPallete?.StrID ?? "");
            }
        }

        public void ProcessFLICK(OngekiFumen fumen, BinaryWriter sb)
        {
            sb.Write(fumen.Flicks.Count);
            foreach (var u in fumen.Flicks.OrderBy(x => x.TGrid))
            {
                sb.Write(u.TGrid.Unit);
                sb.Write(u.TGrid.Grid);
                sb.Write(u.XGrid.Unit);
                sb.Write((int)u.Direction);
                sb.Write(u.IsCritical);
            }
        }

        public void ProcessNOTES(OngekiFumen fumen, BinaryWriter sb)
        {
            var notes = fumen.Taps.OfType<OngekiTimelineObjectBase>().Concat(fumen.Holds).OrderBy(x => x.TGrid).ToArray();
            sb.Write(notes.Length);
            foreach (var u in notes)
            {
                sb.Write(u is WallTap || u is WallHold);
                sb.Write(u.IDShortName);
                switch (u)
                {
                    case Tap t:
                        sb.Write(t.IsCritical);
                        sb.Write(t.ReferenceLaneStart?.RecordId ?? -1);
                        sb.Write(t.TGrid.Unit);
                        sb.Write(t.TGrid.Grid);
                        sb.Write(t.XGrid.Unit);
                        sb.Write(t.XGrid.Grid);
                        break;
                    case Hold h:
                        var end = h.Children.LastOrDefault();
                        sb.Write(h.IsCritical);
                        sb.Write(h.ReferenceLaneStart?.RecordId ?? -1);
                        sb.Write(h.TGrid.Unit);
                        sb.Write(h.TGrid.Grid);
                        sb.Write(h.XGrid.Unit);
                        sb.Write(h.XGrid.Grid);
                        sb.Write(end.TGrid.Unit);
                        sb.Write(end.TGrid.Grid);
                        sb.Write(end.XGrid.Unit);
                        sb.Write(end.XGrid.Grid);
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
