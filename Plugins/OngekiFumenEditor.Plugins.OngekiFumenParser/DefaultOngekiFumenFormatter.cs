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

        public Task<string> SerializeAsync(OngekiFumen fumen)
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

            return Task.FromResult(sb.ToString());
        }

        public void ProcessHEADER(OngekiFumen fumen, StringBuilder sb)
        {
            var metaInfo = fumen.MetaInfo;

            sb.AppendLine("[HEADER]");
            sb.AppendLine($"VERSION {metaInfo.Version.Major} {metaInfo.Version.Minor} {metaInfo.Version.Build}");
            sb.AppendLine($"CREATOR {metaInfo.Creator}");
            sb.AppendLine($"BPM_DEF {metaInfo.BpmDefinition.First} {metaInfo.BpmDefinition.Common} {metaInfo.BpmDefinition.Maximum} {metaInfo.BpmDefinition.Minimum}");
            sb.AppendLine($"MET_DEF {metaInfo.MeterDefinition.Bunshi} {metaInfo.MeterDefinition.Bunbo}");
            sb.AppendLine($"TRESOLUTION {metaInfo.TRESOLUTION}");
            sb.AppendLine($"XRESOLUTION {metaInfo.XRESOLUTION}");
            sb.AppendLine($"CLK_DEF {metaInfo.ClickDefinition}");
            sb.AppendLine($"PROGJUDGE_BPM {metaInfo.ProgJudgeBpm}");
            sb.AppendLine($"TUTORIAL {(metaInfo.Tutorial ? 1 : 0)}");
            sb.AppendLine($"BULLET_DAMAGE {metaInfo.BulletDamage:F3}");
            sb.AppendLine($"HARDBULLET_DAMAGE {metaInfo.HardBulletDamage:F3}");
            sb.AppendLine($"DANGERBULLET_DAMAGE {metaInfo.DangerBulletDamage:F3}");
            sb.AppendLine($"BEAM_DAMAGE {metaInfo.BeamDamage:F3}");
        }

        public void ProcessB_PALETTE(OngekiFumen fumen, StringBuilder sb)
        {
            sb.AppendLine("[B_PALETTE]");

            foreach (var bpl in fumen.BulletPalleteList.OrderBy(x => x.StrID))
                sb.AppendLine($"{bpl.IDShortName} {bpl.StrID} {bpl.ShooterValue} {bpl.PlaceOffset} {bpl.TargetValue} {bpl.Speed} {bpl.BulletTypeValue}");
        }


        public void ProcessCOMPOSITION(OngekiFumen fumen, StringBuilder sb)
        {
            sb.AppendLine("[COMPOSITION]");

            foreach (var o in fumen.BpmList.OrderBy(x => x.TGrid).Where(x => x.TGrid != fumen.BpmList.FirstBpm.TGrid))
                sb.AppendLine($"{o.IDShortName} {o.TGrid.Serialize()} {o.BPM}");
            sb.AppendLine();

            foreach (var o in fumen.MeterChanges.OrderBy(x => x.TGrid))
                sb.AppendLine($"{o.IDShortName} {o.TGrid.Serialize()} {o.BunShi} {o.Bunbo}");
            sb.AppendLine();

            foreach (var o in fumen.ClickSEs.OrderBy(x => x.TGrid))
                sb.AppendLine($"{o.IDShortName} {o.TGrid.Serialize()}");
            sb.AppendLine();

            foreach (var o in fumen.EnemySets.OrderBy(x => x.TGrid))
                sb.AppendLine($"{o.IDShortName} {o.TGrid.Serialize()} {o.TagTblValue}");
        }

        public void ProcessLANE(OngekiFumen fumen, StringBuilder sb)
        {
            void Serialize(ConnectableStartObject laneStart)
            {
                void SerializeOutput(ConnectableObjectBase o) => sb.AppendLine($"{o.IDShortName} {o.RecordId} {o.TGrid.Serialize()} {o.XGrid.Serialize()}");

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
                sb.AppendLine($"{u.IDShortName} {u.ReferenceBulletPallete?.StrID} {u.TGrid.Serialize()} {u.XGrid.Serialize()}");
        }

        public void ProcessBEAM(OngekiFumen fumen, StringBuilder sb)
        {
            void SerializeOutput(BeamBase o) => sb.AppendLine($"{o.IDShortName} {o.RecordId} {o.TGrid.Serialize()} {o.XGrid.Serialize()} {o.WidthId}");

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
                sb.AppendLine($"{u.IDShortName} {u.TGrid.Serialize()} {u.XGrid.Serialize()}");
        }

        public void ProcessFLICK(OngekiFumen fumen, StringBuilder sb)
        {
            sb.AppendLine("[FLICK]");   

            foreach (var u in fumen.Flicks.OrderBy(x => x.TGrid))
                sb.AppendLine($"{u.IDShortName} {u.TGrid.Serialize()} {u.XGrid.Serialize()} {(u.Direction == OngekiFumenEditor.Base.OngekiObjects.Flick.FlickDirection.Left ? "L" : "R")}");
        }

        public void ProcessNOTES(OngekiFumen fumen, StringBuilder sb)
        {
            sb.AppendLine("[NOTES]");

            foreach (var u in fumen.Taps.OfType<OngekiTimelineObjectBase>().Concat(fumen.Holds).OrderBy(x => x.TGrid))
            {
                switch (u)
                {
                    case Tap t:
                        sb.AppendLine($"{t.IDShortName} {t.ReferenceLaneStart?.RecordId ?? -1} {t.TGrid.Serialize()} {t.XGrid.Unit} {t.XGrid.Grid}");
                        break;
                    case Hold h:
                        var end = h.Children.LastOrDefault();
                        sb.AppendLine($"{h.IDShortName} {h.ReferenceLaneStart.RecordId} {h.TGrid.Serialize()} {h.XGrid.Unit} {h.XGrid.Grid} {end?.TGrid.Serialize()} {end?.XGrid.Unit} {end?.XGrid.Grid}");
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
