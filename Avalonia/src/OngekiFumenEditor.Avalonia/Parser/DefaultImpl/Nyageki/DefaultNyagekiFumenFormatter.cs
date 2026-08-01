using Injectio.Attributes;
using DereTore.Common;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.EditorObjects;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Utils;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Parser.DefaultImpl.Nyageki
{
    [RegisterSingleton<IFumenSerializable>]
    class DefaultNyagekiFumenFormatter : IFumenSerializable
    {
        public string FileFormatName => DefaultNyagekiFumenParser.FormatName;
        public string[] SupportFumenFileExtensions => DefaultNyagekiFumenParser.FumenFileExtensions;

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

            ProcessSvgPrefabs(fumen, writer);

            ProcessComments(fumen, writer);

            await writer.FlushAsync();
            await memory.FlushAsync();

            return memory.ToArray();
        }

        private static void ProcessSvgPrefabs(OngekiFumen fumen, StreamWriter writer)
        {
            foreach (var svg in fumen.SvgPrefabs.OrderBy(x => x.TGrid))
            {
                var fields = new List<string>
                {
                    $"Type[{svg.IDShortName}]",
                    $"ColorSimilar[{SvgPrefabFormatUtils.Format(svg.ColorSimilar.CurrentValue)}]",
                    $"Rotation[{SvgPrefabFormatUtils.Format(svg.Rotation.CurrentValue)}]",
                    $"EnableColorfulLaneSimilar[{SvgPrefabFormatUtils.Format(svg.EnableColorfulLaneSimilar)}]",
                    $"OffsetX[{SvgPrefabFormatUtils.Format(svg.OffsetX.CurrentValue)}]",
                    $"OffsetY[{SvgPrefabFormatUtils.Format(svg.OffsetY.CurrentValue)}]",
                    $"ShowOriginColor[{SvgPrefabFormatUtils.Format(svg.ShowOriginColor)}]",
                    $"Opacity[{SvgPrefabFormatUtils.Format(svg.Opacity.CurrentValue)}]",
                    $"Brightness[{SvgPrefabFormatUtils.Format(svg.ColorfulLaneBrightness.CurrentValue)}]",
                    $"Scale[{SvgPrefabFormatUtils.Format(svg.Scale)}]",
                    $"Tolerance[{SvgPrefabFormatUtils.Format(svg.Tolerance.CurrentValue)}]",
                    $"T[{SvgPrefabFormatUtils.Format(svg.TGrid.Unit)},{SvgPrefabFormatUtils.Format(svg.TGrid.Grid)}]",
                    $"X[{SvgPrefabFormatUtils.Format(svg.XGrid.Unit)},{SvgPrefabFormatUtils.Format(svg.XGrid.Grid)}]",
                    $"IsForceColorful[{SvgPrefabFormatUtils.Format(svg.IsForceColorful)}]",
                    $"ColorfulLaneColorId[{SvgPrefabFormatUtils.Format(svg.ColorfulLaneColor.Id)}]",
                    $"CurveInterpolaterFactory[{svg.CurveInterpolaterFactory.Name}]"
                };

                switch (svg)
                {
                    case SvgImageFilePrefab image:
                        /*if (string.IsNullOrWhiteSpace(image.SvgFile?.FullName))
                            throw new Exception($"at {svg.TGrid}, SvgImageFilePrefab.SvgFile is empty or null");
                        */
                        fields.Add($"FilePathBase64[{Base64.Encode(image.SvgFile?.FullName ?? string.Empty)}]");
                        break;
                    case SvgStringPrefab text:
                        /*if (string.IsNullOrWhiteSpace(text.Content) || string.IsNullOrWhiteSpace(text.TypefaceName))
                            throw new Exception($"at {svg.TGrid}, SvgStringPrefab.Content/TypefaceName is empty or null");*/
                        fields.Add($"Content[{Base64.Encode(text.Content)}]");
                        fields.Add($"FontSize[{SvgPrefabFormatUtils.Format(text.FontSize)}]");
                        fields.Add($"TypefaceName[{text.TypefaceName}]");
                        fields.Add($"FontColorId[{SvgPrefabFormatUtils.Format(text.ColorfulLaneColor.Id)}]");
                        fields.Add($"ContentFlowDirection[{text.ContentFlowDirection}]");
                        fields.Add($"ContentLineHeight[{SvgPrefabFormatUtils.Format(text.ContentLineHeight)}]");
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported SVG prefab type '{svg.GetType().FullName}'.");
                }

                writer.WriteLine($"SvgPrefab\t:\t{string.Join(", ", fields)}");
            }
            writer.WriteLine();
        }

        private void ProcessComments(OngekiFumen fumen, StreamWriter sb)
        {
            foreach (var comment in fumen.Comments.OrderBy(x => x.TGrid))
                sb.WriteLine($"Comment\t:\t{Base64.Encode(comment.Content)}\t:\tT[{comment.TGrid.Unit},{comment.TGrid.Grid}]");
            sb.WriteLine();
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
                writer.Write("CurveControlPoint\t:\t");
                writer.Write(child.ReferenceStartObject.RecordId);
                writer.Write("\t:\t");
                writer.Write(child.ReferenceStartObject.IDShortName);
                writer.Write("\t:\t");
                writer.Write(child.ReferenceStartObject.Children.ToList().FindIndex(x => x == child));
                writer.Write("\t:\t");
                writer.Write(child.CurvePrecision);
                writer.Write("\t:\t");
                writer.Write(string.Join("\t...\t", child.PathControls.Select(x => $"(T[{x.TGrid.Unit},{x.TGrid.Grid}],X[{x.XGrid.Unit},{x.XGrid.Grid}])")));
                writer.WriteLine();
            }
            writer.WriteLine();
        }

        private void ProcessLANE_BLOCK(OngekiFumen fumen, StreamWriter writer)
        {
            foreach (var blk in fumen.LaneBlocks.OrderBy(x => x.TGrid))
                writer.WriteLine($"LaneBlock\t:\t{blk.Direction}\t:\t(T[{blk.TGrid.Unit},{blk.TGrid.Grid}])\t->\t(T[{blk.EndIndicator.TGrid.Unit},{blk.EndIndicator.TGrid.Grid}])");
            writer.WriteLine();
        }

        public void ProcessHEADER(OngekiFumen fumen, StreamWriter sb)
        {
            var metaInfo = fumen.MetaInfo;

            sb.WriteLine($"Header.{nameof(metaInfo.Version)}\t:\t{metaInfo.Version}");
            sb.WriteLine($"Header.{nameof(metaInfo.Creator)}\t:\t{metaInfo.Creator}");
            sb.WriteLine($"Header.{nameof(metaInfo.BpmDefinition.First)}Bpm\t:\t{metaInfo.BpmDefinition.First}");
            sb.WriteLine($"Header.{nameof(metaInfo.BpmDefinition.Common)}Bpm\t:\t{metaInfo.BpmDefinition.Common}");
            sb.WriteLine($"Header.{nameof(metaInfo.BpmDefinition.Maximum)}Bpm\t:\t{metaInfo.BpmDefinition.Maximum}");
            sb.WriteLine($"Header.{nameof(metaInfo.BpmDefinition.Minimum)}Bpm\t:\t{metaInfo.BpmDefinition.Minimum}");
            sb.WriteLine($"Header.Meter\t:\t{metaInfo.MeterDefinition.Bunshi} / {metaInfo.MeterDefinition.Bunbo}");
            sb.WriteLine($"Header.{nameof(metaInfo.TRESOLUTION)}\t:\t{metaInfo.TRESOLUTION}");
            sb.WriteLine($"Header.{nameof(metaInfo.XRESOLUTION)}\t:\t{metaInfo.XRESOLUTION}");
            sb.WriteLine($"Header.{nameof(metaInfo.ClickDefinition)}\t:\t{metaInfo.ClickDefinition}");
            sb.WriteLine($"Header.{nameof(metaInfo.Tutorial)}\t:\t{metaInfo.Tutorial}");
            sb.WriteLine($"Header.{nameof(metaInfo.BeamDamage)}\t:\t{metaInfo.BeamDamage}");
            sb.WriteLine($"Header.{nameof(metaInfo.HardBulletDamage)}\t:\t{metaInfo.HardBulletDamage}");
            sb.WriteLine($"Header.{nameof(metaInfo.DangerBulletDamage)}\t:\t{metaInfo.DangerBulletDamage}");
            sb.WriteLine($"Header.{nameof(metaInfo.BulletDamage)}\t:\t{metaInfo.BulletDamage}");
            sb.WriteLine($"Header.{nameof(metaInfo.ProgJudgeBpm)}\t:\t{metaInfo.ProgJudgeBpm}");

            sb.WriteLine();
        }

        public void ProcessB_PALETTE(OngekiFumen fumen, StreamWriter sb)
        {
            foreach (var bpl in fumen.BulletPalleteList.OrderBy(x => x.StrID))
            {
                sb.Write($"BulletPallete\t:\t");
                sb.Write(bpl.StrID);
                sb.Write("\t:\t");
                sb.Write($"Target[{bpl.TargetValue}]");
                sb.Write(", ");
                sb.Write($"Size[{bpl.SizeValue}]");
                sb.Write(", ");
                sb.Write($"Type[{bpl.TypeValue}]");
                sb.Write(", ");
                sb.Write($"Speed[{bpl.Speed}]");
                sb.Write(", ");
                sb.Write($"RandomOffsetRange[{bpl.RandomOffsetRange}]");
                sb.Write(", ");
                sb.Write($"PlaceOffset[{bpl.PlaceOffset}]");
                sb.Write(", ");
                sb.Write($"Shooter[{bpl.ShooterValue}]");
                sb.WriteLine();
            }
            sb.WriteLine();
        }

        public void ProcessCOMPOSITION(OngekiFumen fumen, StreamWriter sb)
        {
            foreach (var clk in fumen.ClickSEs.OrderBy(x => x.TGrid))
                sb.WriteLine($"ClickSE\t:\tT[{clk.TGrid.Unit},{clk.TGrid.Grid}]");
            foreach (var est in fumen.EnemySets.OrderBy(x => x.TGrid))
                sb.WriteLine($"EnemySet\t:\t{est.TagTblValue}\t:\tT[{est.TGrid.Unit},{est.TGrid.Grid}]");
            foreach (var met in fumen.MeterChanges.OrderBy(x => x.TGrid).Where(x => x.TGrid != fumen.MeterChanges.FirstMeter.TGrid))
                sb.WriteLine($"MeterChange\t:\t{met.BunShi}/{met.Bunbo}\t:\tT[{met.TGrid.Unit},{met.TGrid.Grid}]");
            sb.WriteLine();
            foreach (var bpm in fumen.BpmList.OrderBy(x => x.TGrid).Where(x => x.TGrid != TGrid.Zero))
                sb.WriteLine($"BpmChange\t:\t{bpm.BPM}\t:\tT[{bpm.TGrid.Unit},{bpm.TGrid.Grid}]");
            sb.WriteLine();

            foreach (var soflans in fumen.SoflansMap.Values)
            {
                foreach (var soflan in soflans.OrderBy(x => x.TGrid))
                {
                    var name = soflan switch
                    {
                        KeyframeSoflan => "KeyframeSoflan",
                        InterpolatableSoflan => "InterpolatableSoflan",
                        Soflan => "Soflan"
                    };
                    sb.Write($"{name}\t:\t{soflan.Speed}\t:\t(T[{soflan.TGrid.Unit},{soflan.TGrid.Grid}])\t->\t(T[{soflan.EndTGrid.Unit},{soflan.EndTGrid.Grid}])");
                    if (soflan is InterpolatableSoflan isf)
                        sb.Write($": EndSpeed[{(isf.EndIndicator as InterpolatableSoflan.InterpolatableSoflanIndicator).Speed}], Easing[{isf.Easing}]");
                    sb.Write($": SoflanGroup[{soflan.SoflanGroup}]");
                    sb.WriteLine();
                    //todo add soflanGroup
                }
                sb.WriteLine();
            }

            foreach (var isfList in fumen.IndividualSoflanAreaMap.Values)
            {
                foreach (var isf in isfList.OrderBy(x => x.TGrid))
                {
                    sb.Write($"{nameof(IndividualSoflanArea)}\t:\t(T[{isf.TGrid.Unit},{isf.TGrid.Grid}])\t->\t(T[{isf.EndIndicator.TGrid.Unit},{isf.EndIndicator.TGrid.Grid}])");
                    sb.Write($"\t:\t(X[{isf.XGrid.Unit},{isf.XGrid.Grid}])\t->\t(X[{isf.EndIndicator.XGrid.Unit},{isf.EndIndicator.XGrid.Grid}])");
                    sb.Write($"\t:\tSoflanGroup[{isf.SoflanGroup}]");
                    sb.WriteLine();
                }
                sb.WriteLine();
            }
        }

        public void ProcessLANE(OngekiFumen fumen, StreamWriter sb)
        {
            var builder = new StringBuilder();

            string Serialize(LaneStartBase laneStart)
            {
                builder.Clear();
                builder.Append($"Lane\t:\t{laneStart.RecordId}\t:\t");

                string SerializeOutput(ConnectableObjectBase o)
                {
                    return $"(Type[{o.IDShortName}], X[{o.XGrid.Unit},{o.XGrid.Grid}], {nameof(laneStart.IsTransparent)}[{laneStart.IsTransparent}] , T[{o.TGrid.Unit},{o.TGrid.Grid}]{(o is IColorfulLane c ? $", C[{c.ColorId.Name},{c.Brightness}]" : string.Empty)})";
                }

                var r = string.Join("\t->\t", laneStart.Children.AsEnumerable<ConnectableObjectBase>().Prepend(laneStart).Select(x => SerializeOutput(x)));
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
            {
                if (bullet.ReferenceBulletPallete is { } pallete && pallete != BulletPallete.DummyCustomPallete)
                {
                    sb.WriteLine($"Bullet\t:\t{bullet.ReferenceBulletPallete?.StrID}\t:\tX[{bullet.XGrid.Unit},{bullet.XGrid.Grid}], T[{bullet.TGrid.Unit},{bullet.TGrid.Grid}], D[{bullet.BulletDamageTypeValue}]");
                }
                else
                {
                    sb.Write($"CustomBullet\t:\tX[{bullet.XGrid.Unit},{bullet.XGrid.Grid}], T[{bullet.TGrid.Unit},{bullet.TGrid.Grid}], D[{bullet.BulletDamageTypeValue}]");
                    sb.Write($", Speed[{bullet.Speed}]");
                    sb.Write($", PlaceOffset[{bullet.PlaceOffset}]");
                    sb.Write($", RandomOffsetRange[{bullet.RandomOffsetRange}]");
                    sb.Write($", TypeValue[{bullet.TypeValue}]");
                    sb.Write($", SizeValue[{bullet.SizeValue}]");
                    sb.Write($", ShooterValue[{bullet.ShooterValue}]");
                    sb.Write($", TargetValue[{bullet.TargetValue}]");
                    sb.WriteLine($", Speed[{bullet.Speed}]");
                }
            }
            sb.WriteLine();
        }

        public void ProcessBEAM(OngekiFumen fumen, StreamWriter sb)
        {
            var builder = new StringBuilder();

            string Serialize(ConnectableStartObject laneStart)
            {
                builder.Clear();
                builder.Append($"Beam\t:\t{laneStart.RecordId}:");

                string SerializeOutput(ConnectableObjectBase o)
                {
                    var b = ((IBeamObject)o);
                    var r = $"(Type[{o.IDShortName}], X[{o.XGrid.Unit},{o.XGrid.Grid}], T[{o.TGrid.Unit},{o.TGrid.Grid}], W[{b.WidthId.Id}]";
                    if (b.ObliqueSourceXGridOffset is not null)
                        r += $", OX[{b.ObliqueSourceXGridOffset.Unit},{b.ObliqueSourceXGridOffset.Grid}]";
                    return r + ")";
                }

                var r = string.Join("\t->\t", laneStart.Children.AsEnumerable<ConnectableObjectBase>().Prepend(laneStart).Select(x => SerializeOutput(x)));
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
            {
                if (bell.ReferenceBulletPallete != BulletPallete.DummyCustomPallete)
                {
                    sb.WriteLine($"Bell\t:\t{bell.ReferenceBulletPallete?.StrID}\t:\tX[{bell.XGrid.Unit},{bell.XGrid.Grid}], T[{bell.TGrid.Unit},{bell.TGrid.Grid}]");
                }
                else
                {
                    sb.Write($"CustomBell\t:\tX[{bell.XGrid.Unit},{bell.XGrid.Grid}], T[{bell.TGrid.Unit},{bell.TGrid.Grid}]");

                    sb.Write($", Speed[{bell.Speed}]");
                    sb.Write($", PlaceOffset[{bell.PlaceOffset}]");
                    sb.Write($", RandomOffsetRange[{bell.RandomOffsetRange}]");
                    sb.Write($", TypeValue[{bell.TypeValue}]");
                    sb.Write($", SizeValue[{bell.SizeValue}]");
                    sb.Write($", ShooterValue[{bell.ShooterValue}]");
                    sb.WriteLine($", TargetValue[{bell.TargetValue}]");
                }
            }
            sb.WriteLine();
        }

        public void ProcessFLICK(OngekiFumen fumen, StreamWriter sb)
        {
            foreach (var flick in fumen.Flicks.OrderBy(x => x.TGrid))
                sb.WriteLine($"Flick\t:\tX[{flick.XGrid.Unit},{flick.XGrid.Grid}], T[{flick.TGrid.Unit},{flick.TGrid.Grid}], C[{flick.IsCritical}], D[{flick.Direction}]");
            sb.WriteLine();
        }

        public void ProcessNOTES(OngekiFumen fumen, StreamWriter sb)
        {
            foreach (var tap in fumen.Taps.OrderBy(x => x.TGrid))
                sb.WriteLine($"Tap\t:\t{tap.ReferenceLaneStrId}\t:\tX[{tap.XGrid.Unit},{tap.XGrid.Grid}], T[{tap.TGrid.Unit},{tap.TGrid.Grid}], C[{tap.IsCritical}]");
            sb.WriteLine();
            foreach (var hold in fumen.Holds.OrderBy(x => x.TGrid))
            {
                sb.Write($"Hold\t:\t{hold.ReferenceLaneStrId}, {hold.IsCritical}, {hold.IsWallHold}\t:\t(X[{hold.XGrid.Unit},{hold.XGrid.Grid}], T[{hold.TGrid.Unit}, {hold.TGrid.Grid}])");
                if (hold.HoldEnd is HoldEnd end)
                    sb.Write($"\t->\t(X[{end.XGrid.Unit},{end.XGrid.Grid}], T[{end.TGrid.Unit},{end.TGrid.Grid}])");
                sb.WriteLine();
            }
            sb.WriteLine();
        }
    }
}


