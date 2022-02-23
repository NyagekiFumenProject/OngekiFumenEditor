using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Parser;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditorPlugins.OngekiFumenParser
{
    [Export(typeof(IFumenSerializable))]
    public class DefaultOngekiFumenFormatter : IFumenSerializable
    {
        public string FileFormatName => DefaultOngekiFumenParser.FormatName;
        public string[] SupportFumenFileExtensions => DefaultOngekiFumenParser.FumenFileExtensions;

        public Task<byte[]> SerializeAsync(OngekiFumen fumen)
        {
            var sb = new StringBuilder();

            ProcessHEADER(fumen, sb);
            sb.AppendLine();

            ProcessB_PALETTE(fumen, sb);
            sb.AppendLine();

            ProcessCOMPOSITION(fumen, sb);
            sb.AppendLine();

            ProcessLANE(fumen, sb);
            sb.AppendLine();

            ProcessBULLET(fumen, sb);
            sb.AppendLine();

            ProcessBEAM(fumen, sb);
            sb.AppendLine();

            ProcessBELL(fumen, sb);
            sb.AppendLine();

            ProcessFLICK(fumen, sb);
            sb.AppendLine();

            ProcessNOTES(fumen, sb);
            sb.AppendLine();

            return Task.FromResult(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        public void ProcessHEADER(OngekiFumen fumen, StringBuilder sb)
        {
            var metaInfo = fumen.MetaInfo;

            sb.AppendLine("[HEADER]");
            //sb.AppendLine($"VERSION\t{metaInfo.Version.Major}\t{metaInfo.Version.Minor}\t{metaInfo.Version.Build}");
            sb.AppendLine($"VERSION\t{1}\t{2}\t{0}");
            sb.AppendLine($"CREATOR\t{metaInfo.Creator}");
            sb.AppendLine($"BPM_DEF\t{metaInfo.BpmDefinition.First}\t{metaInfo.BpmDefinition.Common}\t{metaInfo.BpmDefinition.Maximum}\t{metaInfo.BpmDefinition.Minimum}");
            sb.AppendLine($"MET_DEF\t{metaInfo.MeterDefinition.Bunshi}\t{metaInfo.MeterDefinition.Bunbo}");
            sb.AppendLine($"TRESOLUTION\t{metaInfo.TRESOLUTION}");
            sb.AppendLine($"XRESOLUTION\t{metaInfo.XRESOLUTION}");
            sb.AppendLine($"CLK_DEF\t{metaInfo.ClickDefinition}");
            sb.AppendLine($"PROGJUDGE_BPM\t{metaInfo.ProgJudgeBpm}");
            sb.AppendLine($"TUTORIAL\t{(metaInfo.Tutorial ? 1 : 0)}");
            sb.AppendLine($"BULLET_DAMAGE\t{metaInfo.BulletDamage:F3}");
            sb.AppendLine($"HARDBULLET_DAMAGE\t{metaInfo.HardBulletDamage:F3}");
            sb.AppendLine($"DANGERBULLET_DAMAGE\t{metaInfo.DangerBulletDamage:F3}");
            sb.AppendLine($"BEAM_DAMAGE\t{metaInfo.BeamDamage:F3}");
        }

        public void ProcessB_PALETTE(OngekiFumen fumen, StringBuilder sb)
        {
            sb.AppendLine("[B_PALETTE]");

            foreach (var bpl in fumen.BulletPalleteList.OrderBy(x => x.StrID))
                sb.AppendLine($"{bpl.IDShortName}\t{bpl.StrID}\t{bpl.ShooterValue}\t{bpl.PlaceOffset}\t{bpl.TargetValue}\t{bpl.Speed}\t{bpl.BulletTypeValue}");
        }


        public void ProcessCOMPOSITION(OngekiFumen fumen, StringBuilder sb)
        {
            sb.AppendLine("[COMPOSITION]");

            foreach (var o in fumen.BpmList.OrderBy(x => x.TGrid).Where(x => x.TGrid != fumen.BpmList.FirstBpm.TGrid))
                sb.AppendLine($"{o.IDShortName}\t{o.TGrid.Serialize()}\t{o.BPM}");
            sb.AppendLine();

            foreach (var o in fumen.MeterChanges.OrderBy(x => x.TGrid).Where(x => x.TGrid != fumen.MeterChanges.FirstMeter.TGrid))
                sb.AppendLine($"{o.IDShortName}\t{o.TGrid.Serialize()}\t{o.BunShi}\t{o.Bunbo}");
            sb.AppendLine();

            foreach (var o in fumen.ClickSEs.OrderBy(x => x.TGrid))
                sb.AppendLine($"{o.IDShortName}\t{o.TGrid.Serialize()}");
            sb.AppendLine();

            foreach (var o in fumen.EnemySets.OrderBy(x => x.TGrid))
                sb.AppendLine($"{o.IDShortName}\t{o.TGrid.Serialize()}\t{o.TagTblValue}");
        }

        public void ProcessLANE(OngekiFumen fumen, StringBuilder sb)
        {
            void Serialize(ConnectableStartObject laneStart)
            {
                void SerializeOutput(ConnectableObjectBase o) => sb.AppendLine($"{o.IDShortName}\t{o.RecordId}\t{o.TGrid.Serialize()}\t{o.XGrid.Serialize()}");

                SerializeOutput(laneStart);
                foreach (var child in laneStart.Children)
                    SerializeOutput(child);
            }

            sb.AppendLine("[LANE]");
            foreach (var group in fumen.Lanes.GroupBy(x => x.LaneType).OrderByDescending(x => x.Key))
            {
                foreach (var laneStart in group.OrderBy(x => x.RecordId))
                {
                    Serialize(laneStart);
                    sb.AppendLine();
                }
            }
        }

        public void ProcessBULLET(OngekiFumen fumen, StringBuilder sb)
        {
            sb.AppendLine("[BULLET]");
            foreach (var u in fumen.Bullets.OrderBy(x => x.TGrid))
                sb.AppendLine($"{u.IDShortName}\t{u.ReferenceBulletPallete?.StrID}\t{u.TGrid.Serialize()}\t{u.XGrid.Serialize()}");
        }

        public void ProcessBEAM(OngekiFumen fumen, StringBuilder sb)
        {
            void SerializeOutput(BeamBase o) => sb.AppendLine($"{o.IDShortName}\t{o.RecordId}\t{o.TGrid.Serialize()}\t{o.XGrid.Serialize()}\t{o.WidthId}");

            sb.AppendLine("[BEAM]");
            foreach (var beamStart in fumen.Beams.OrderBy(x => x.RecordId))
            {
                SerializeOutput(beamStart);
                foreach (var child in beamStart.Children)
                    SerializeOutput(child);
            }
        }

        public void ProcessBELL(OngekiFumen fumen, StringBuilder sb)
        {
            sb.AppendLine("[BELL]");

            foreach (var u in fumen.Bells.OrderBy(x => x.TGrid))
                sb.AppendLine($"{u.IDShortName}\t{u.TGrid.Serialize()}\t{u.XGrid.Serialize()}");
        }

        public void ProcessFLICK(OngekiFumen fumen, StringBuilder sb)
        {
            sb.AppendLine("[FLICK]");

            foreach (var u in fumen.Flicks.OrderBy(x => x.TGrid))
                sb.AppendLine($"{u.IDShortName}\t{u.TGrid.Serialize()}\t{u.XGrid.Serialize()}\t{(u.Direction == Flick.FlickDirection.Left ? "L" : "R")}");
        }

        public void ProcessNOTES(OngekiFumen fumen, StringBuilder sb)
        {
            sb.AppendLine("[NOTES]");

            foreach (var u in fumen.Taps.OfType<OngekiTimelineObjectBase>().Concat(fumen.Holds).OrderBy(x => x.TGrid))
            {
                switch (u)
                {
                    case Tap t:
                        sb.AppendLine($"{t.IDShortName}\t{t.ReferenceLaneStart?.RecordId ?? -1}\t{t.TGrid.Serialize()}\t{t.XGrid.Unit}\t{t.XGrid.Grid}");
                        break;
                    case Hold h:
                        var end = h.Children.LastOrDefault();
                        sb.AppendLine($"{h.IDShortName}\t{h.ReferenceLaneStart?.RecordId ?? -1}\t{h.TGrid.Serialize()}\t{h.XGrid.Unit}\t{h.XGrid.Grid}\t{end?.TGrid.Serialize()}\t{end?.XGrid.Unit}\t{end?.XGrid.Grid}");
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
