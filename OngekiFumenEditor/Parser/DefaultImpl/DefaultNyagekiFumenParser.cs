using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static OngekiFumenEditor.Base.OngekiObjects.BulletPallete;
using static OngekiFumenEditor.Base.OngekiObjects.EnemySet;
using static OngekiFumenEditor.Base.OngekiObjects.Flick;

namespace OngekiFumenEditor.Parser.DefaultImpl
{
    [Export(typeof(IFumenDeserializable))]
    public class DefaultNyagekiFumenParser : IFumenDeserializable
    {
        public const string FormatName = "Nyageki Fumen File";
        public string FileFormatName => FormatName;

        public static readonly string[] FumenFileExtensions = new[] { ".nyageki" };
        public string[] SupportFumenFileExtensions => FumenFileExtensions;

        public Task<OngekiFumen> DeserializeAsync(Stream stream)
        {
            using var gzipDecompress = new GZipStream(stream, CompressionMode.Decompress);
            using var reader = new BinaryReader(gzipDecompress);

            var fumen = new OngekiFumen();

            ProcessHEADER(fumen, reader);

            ProcessB_PALETTE(fumen, reader);

            ProcessCOMPOSITION(fumen, reader);

            ProcessLANE(fumen, reader);

            ProcessBULLET(fumen, reader);

            ProcessBEAM(fumen, reader);

            ProcessBELL(fumen, reader);

            ProcessFLICK(fumen, reader);

            ProcessNOTES(fumen, reader);

            fumen.Setup();

            return Task.FromResult(fumen);
        }


        public void ProcessHEADER(OngekiFumen fumen, BinaryReader reader)
        {
            var metaInfo = fumen.MetaInfo;

            var major = reader.ReadInt32();
            var minor = reader.ReadInt32();
            var build = reader.ReadInt32();
            metaInfo.Version = new Version(major, minor, build);

            metaInfo.Creator = reader.ReadString();

            metaInfo.BpmDefinition.First = reader.ReadDouble();
            metaInfo.BpmDefinition.Common = reader.ReadDouble();
            metaInfo.BpmDefinition.Maximum = reader.ReadDouble();
            metaInfo.BpmDefinition.Minimum = reader.ReadDouble();

            metaInfo.MeterDefinition.Bunshi = reader.ReadInt32();
            metaInfo.MeterDefinition.Bunbo = reader.ReadInt32();

            metaInfo.TRESOLUTION = reader.ReadInt32();
            metaInfo.XRESOLUTION = reader.ReadInt32();

            metaInfo.ClickDefinition = reader.ReadInt32();

            metaInfo.ProgJudgeBpm = reader.ReadSingle();

            metaInfo.Tutorial = reader.ReadBoolean();

            metaInfo.BulletDamage = reader.ReadDouble();
            metaInfo.HardBulletDamage = reader.ReadDouble();
            metaInfo.DangerBulletDamage = reader.ReadDouble();
            metaInfo.BeamDamage = reader.ReadDouble();
        }

        public void ProcessB_PALETTE(OngekiFumen fumen, BinaryReader reader)
        {
            var count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var bpl = new BulletPallete();
                bpl.StrID = reader.ReadString();
                bpl.ShooterValue = reader.ReadString() switch
                {
                    "UPS" => Shooter.TargetHead,
                    "ENE" => Shooter.Enemy,
                    "CEN" => Shooter.Center,
                    _ => default
                };
                bpl.PlaceOffset = reader.ReadInt32();
                bpl.TargetValue = reader.ReadString() switch
                {
                    "PLR" => Target.Player,
                    "FIX" => Target.FixField,
                    _ => default
                };
                bpl.Speed = reader.ReadSingle();
                bpl.BulletTypeValue = reader.ReadString() switch
                {
                    "NML" => BulletType.Normal,
                    "STR" => BulletType.Hard,
                    "DNG" => BulletType.Danger,
                    _ => default
                };

                fumen.AddObject(bpl);
            }
        }

        public void ProcessCOMPOSITION(OngekiFumen fumen, BinaryReader reader)
        {
            var count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var bpm = new BPMChange();
                bpm.TGrid.Unit = reader.ReadSingle();
                bpm.TGrid.Grid = reader.ReadInt32();
                bpm.BPM = reader.ReadDouble();

                fumen.AddObject(bpm);
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var met = new MeterChange();

                met.TGrid.Unit = reader.ReadSingle();
                met.TGrid.Grid = reader.ReadInt32();
                met.BunShi = reader.ReadInt32();
                met.Bunbo = reader.ReadInt32();

                fumen.AddObject(met);
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var clk = new ClickSE();
                clk.TGrid.Unit = reader.ReadSingle();
                clk.TGrid.Grid = reader.ReadInt32();

                fumen.AddObject(clk);
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var est = new EnemySet();
                est.TGrid.Unit = reader.ReadSingle();
                est.TGrid.Grid = reader.ReadInt32();
                est.TagTblValue = reader.ReadString() switch
                {
                    "BOSS" => WaveChangeConst.Boss,
                    "WAVE1" => WaveChangeConst.Wave1,
                    "WAVE2" => WaveChangeConst.Wave2,
                    _ => default
                };

                fumen.AddObject(est);
            }
        }

        public void ProcessLANE(OngekiFumen fumen, BinaryReader reader)
        {
            ConnectableStartObject DeSerialize()
            {
                ConnectableObjectBase DeSerializeInput()
                {
                    var id = reader.ReadString();
                    ConnectableObjectBase connectObject = id switch
                    {
                        "WLS" => new WallLeftStart(),
                        "WLN" => new WallLeftNext(),
                        "WLE" => new WallLeftEnd(),

                        "WRS" => new WallRightStart(),
                        "WRN" => new WallRightNext(),
                        "WRE" => new WallRightEnd(),

                        "LRS" => new LaneRightStart(),
                        "LRN" => new LaneRightNext(),
                        "LRE" => new LaneRightEnd(),

                        "LLS" => new LaneLeftStart(),
                        "LLN" => new LaneLeftNext(),
                        "LLE" => new LaneLeftEnd(),

                        "LCS" => new LaneCenterStart(),
                        "LCN" => new LaneCenterNext(),
                        "LCE" => new LaneCenterEnd(),

                        _ => default
                    };

                    if (connectObject is null)
                        return default;

                    connectObject.RecordId = reader.ReadInt32();
                    connectObject.TGrid.Unit = reader.ReadSingle();
                    connectObject.TGrid.Grid = reader.ReadInt32();
                    connectObject.XGrid.Unit = reader.ReadSingle();

                    return connectObject;
                }

                var childCount = reader.ReadInt32();
                var laneStart = DeSerializeInput() as ConnectableStartObject;
                for (int i = 0; i < childCount; i++)
                {
                    var child = DeSerializeInput() as ConnectableChildObjectBase;
                    laneStart.AddChildObject(child);
                }

                return laneStart;
            }

            var count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var laneStartsCount = reader.ReadInt32();

                for (int y = 0; y < laneStartsCount; y++)
                {
                    var laneStart = DeSerialize();
                    fumen.AddObject(laneStart);
                }
            }
        }

        public void ProcessBULLET(OngekiFumen fumen, BinaryReader reader)
        {
            var count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var blt = new Bullet();

                var strId = reader.ReadString();
                blt.ReferenceBulletPallete = fumen.BulletPalleteList.FirstOrDefault(x => x.StrID == strId);
                blt.TGrid.Unit = reader.ReadSingle();
                blt.TGrid.Grid = reader.ReadInt32();
                blt.XGrid.Unit = reader.ReadSingle();

                fumen.AddObject(blt);
            }
        }

        public void ProcessBEAM(OngekiFumen fumen, BinaryReader reader)
        {
            BeamBase DeSerializeInput()
            {
                BeamBase beamObject = reader.ReadString() switch
                {
                    "BMS" => new BeamStart(),
                    "BMN" => new BeamNext(),
                    "BME" => new BeamEnd(),
                    _ => default,
                };

                beamObject.RecordId = reader.ReadInt32();
                beamObject.TGrid.Unit = reader.ReadSingle();
                beamObject.TGrid.Grid = reader.ReadInt32();
                beamObject.XGrid.Unit = reader.ReadSingle();
                beamObject.WidthId = reader.ReadInt32();

                return beamObject;
            }

            var count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var childCount = reader.ReadInt32();
                var beamStart = DeSerializeInput() as BeamStart;
                for (int y = 0; y < childCount; y++)
                    beamStart.AddChildBeamObject(DeSerializeInput() as BeamChildBase);

                fumen.AddObjects(beamStart);
            }
        }

        public void ProcessBELL(OngekiFumen fumen, BinaryReader reader)
        {
            var count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var bel = new Bell();

                bel.TGrid.Unit = reader.ReadSingle();
                bel.TGrid.Grid = reader.ReadInt32();
                bel.XGrid.Unit = reader.ReadSingle();

                fumen.AddObject(bel);
            }
        }

        public void ProcessFLICK(OngekiFumen fumen, BinaryReader reader)
        {
            var count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var flk = new Flick();

                flk.TGrid.Unit = reader.ReadSingle();
                flk.TGrid.Grid = reader.ReadInt32();
                flk.XGrid.Unit = reader.ReadSingle();
                flk.Direction = (FlickDirection)reader.ReadInt32();
                flk.IsCritical = reader.ReadBoolean();

                fumen.AddObject(flk);
            }
        }

        public void ProcessNOTES(OngekiFumen fumen, BinaryReader reader)
        {
            var count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var isWall = reader.ReadBoolean();
                OngekiMovableObjectBase obj = reader.ReadString() switch
                {
                    "TAP" or "XTP" or "CTP" => isWall ? new WallTap() : new Tap(),
                    "HLD" or "CHD" or "XHD" => isWall ? new WallHold() : new Hold(),
                    _ => default
                };

                var isCritical = reader.ReadBoolean();
                var recordId = reader.ReadInt32();

                ((ILaneDockable)obj).ReferenceLaneStart = fumen.Lanes.FirstOrDefault(x => x.RecordId == recordId);
                obj.TGrid.Unit = reader.ReadSingle();
                obj.TGrid.Grid = reader.ReadInt32();
                obj.XGrid.Unit = reader.ReadSingle();
                obj.XGrid.Grid = reader.ReadInt32();

                switch (obj)
                {
                    case Tap tap:
                        tap.IsCritical = isCritical;
                        break;
                    case Hold hold:
                        hold.IsCritical = isCritical;
                        var holdEnd = isWall ? new WallHoldEnd() : new HoldEnd();
                        holdEnd.TGrid.Unit = reader.ReadSingle();
                        holdEnd.TGrid.Grid = reader.ReadInt32();
                        holdEnd.XGrid.Unit = reader.ReadSingle();
                        holdEnd.XGrid.Grid = reader.ReadInt32();
                        hold.AddChildObject(holdEnd);
                        break;
                    default:
                        break;
                }

                fumen.AddObject(obj);
            }
        }
    }
}
