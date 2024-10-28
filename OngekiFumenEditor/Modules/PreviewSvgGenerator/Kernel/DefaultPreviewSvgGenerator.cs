using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.EditorObjects.Svg;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using OngekiFumenEditor.Utils.Ogkr;
using OpenTK.Mathematics;
using SharpVectors.Renderers;
using Svg;
using Svg.FilterEffects;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Base.OngekiObjects.Bullet;
using static OngekiFumenEditor.Utils.MathUtils;
using Rectangle = System.Drawing.Rectangle;
using SvgImage = Svg.SvgImage;

namespace OngekiFumenEditor.Modules.PreviewSvgGenerator.Kernel
{
    [Export(typeof(IPreviewSvgGenerator))]
    public class DefaultPreviewSvgGenerator : IPreviewSvgGenerator
    {
        private SoflanList GenerateWeightedSoflan(SoflanList soflans, SvgGenerateOption opt)
        {
            var offset = opt.WeightedSoflanOffset;
            var stress = opt.WeightedSoflanStress;
            var slope = opt.WeightedSoflanSlope;

            float sigmoid(float x)
            {
                return 1 - stress / (1 + MathF.Pow(MathF.E, -slope * (x - offset)));
            }

            var newList = new SoflanList();
            foreach (var sfl in soflans.OfType<OngekiTimelineObjectBase>())
            {
                var clone = sfl.CopyNew() as ISoflan;
                if (sfl is Soflan sf && clone is Soflan d)
                    d.CopyEntire(sf);

                var beforeSoflan = clone.Speed;
                var weight = sigmoid(MathF.Abs(beforeSoflan));
                if (weight > 0.999)
                    weight = 1;
                var afterSoflan = beforeSoflan * weight;

                //Log.LogDebug($" {beforeSoflan:F2} * {weight:F2} --> {afterSoflan:F2}  {sfl.TGrid} {clone.TGrid}");

                clone.Speed = afterSoflan;
                newList.Add(clone);
            }

            return newList;
        }

        public async Task<byte[]> GenerateSvgAsync(OngekiFumen rawFumen, SvgGenerateOption option)
        {
            var svgDocument = new SvgDocument();

            var fumen = await StandardizeFormat.CopyFumenObject(rawFumen);
            if (option.SoflanMode == SoflanMode.AbsSoflan)
            {
                foreach (var sfl in fumen.Soflans)
                    sfl.ApplySpeedInDesignMode = true;
            }
            var specifySoflans = option.SoflanMode != SoflanMode.WeightedSoflan ? fumen.Soflans : GenerateWeightedSoflan(fumen.Soflans, option);

            var totalWidth = option.ViewWidth;
            var maxTGrid = TGridCalculator.ConvertAudioTimeToTGrid(option.Duration, fumen.BpmList);

            double totalHeight;
            if (option.SoflanMode == SoflanMode.Soflan || option.SoflanMode == SoflanMode.WeightedSoflan)
                totalHeight = TGridCalculator.ConvertTGridToY_PreviewMode(maxTGrid, specifySoflans, fumen.BpmList, option.VerticalScale);
            else
                totalHeight = TGridCalculator.ConvertTGridToY_DesignMode(maxTGrid, fumen.Soflans, fumen.BpmList, option.VerticalScale);

            svgDocument.Width = new SvgUnit(SvgUnitType.Pixel, (float)totalWidth);
            svgDocument.Height = new SvgUnit(SvgUnitType.Pixel, (float)totalHeight);
            //svgDocument.AddStyle("background", "black", 0);

            var effect = GenerateCriticalEffect();
            effect.ID = "criticalEffect";
            svgDocument.Children.Add(effect);

            var ctx = new GenerateContext()
            {
                Document = svgDocument,
                Fumen = fumen,
                SpecifySoflans = specifySoflans,
                Option = option,
                TotalHeight = totalHeight,
                MaxTGrid = maxTGrid,
            };
            await SerializeFumenToSvg(ctx);

            byte[] buf = default;
            if (option.RenderAsPng)
            {
                using var image = new Bitmap((int)ctx.TotalWidth, /*(int)ctx.TotalHeight*/60000, PixelFormat.Format32bppArgb);
                using var grahics = Graphics.FromImage(image);
                using var render = SvgRenderer.FromGraphics(grahics);
                render.SmoothingMode = SmoothingMode.AntiAlias;
                var matrix = render.Transform;
                matrix.Translate(0, 30000);

                svgDocument.RenderElement(render);

                grahics.Dispose();

                using var ms = new MemoryStream();
                image.Save(ms, ImageFormat.Png);
                svgDocument.Write(ms, false);
                ms.Seek(0, SeekOrigin.Begin);
                buf = ms.ToArray();
            }
            else
            {
                using var ms = new MemoryStream();
                svgDocument.Write(ms, false);
                ms.Seek(0, SeekOrigin.Begin);
                buf = ms.ToArray();
            }

            if (!string.IsNullOrWhiteSpace(option.OutputFilePath))
                await File.WriteAllBytesAsync(option.OutputFilePath, buf);
            return buf;
        }

        private SvgElement GenerateCriticalEffect()
        {
            var r = new SvgFilter();
            r.Children.Add(new SvgFlood()
            {
                FloodColor = new SvgColourServer(Color.FromArgb(255, 255, 255, 128)),
                FloodOpacity = 1f,
                Input = "SourceGraphic"
            });
            r.Children.Add(new SvgComposite()
            {
                Operator = SvgCompositeOperator.In,
                Input2 = "SourceGraphic"
            });
            r.Children.Add(new SvgGaussianBlur()
            {
                StdDeviation = new SvgNumberCollection()
                {
                    2
                }
            });
            var transfer = new SvgComponentTransfer()
            {
                Result = "glow1",
            };
            transfer.Children.Add(new SvgFuncA()
            {
                Type = SvgComponentTransferType.Linear,
                Slope = 10,
                Intercept = 0
            });
            r.Children.Add(transfer);
            var merge = new SvgMerge();
            merge.Children.Add(new SvgMergeNode()
            {
                Input = "glow1"
            });
            merge.Children.Add(new SvgMergeNode()
            {
                Input = "SourceGraphic"
            });
            r.Children.Add(merge);

            return r;
        }

        private async Task SerializeFumenToSvg(GenerateContext ctx)
        {
            await SerializePlayField(ctx);
            await SerializeEvents(ctx);
            await SerializeLanes(ctx);
            await SerializeTap(ctx);
            await SerializeBell(ctx);
            await SerializeBeams(ctx);
        }

        private async Task SerializeBeams(GenerateContext ctx)
        {
            var group = new SvgGroup();
            group.AddCustomClass("beamGroup");
            var def = new SvgDefinitionList();
            var pattern = new SvgPatternServer()
            {
                ID = "beamBody",
                PatternUnits = SvgCoordinateUnits.UserSpaceOnUse,
                Width = new SvgUnit(SvgUnitType.Pixel, 4),
                Height = new SvgUnit(SvgUnitType.Pixel, 4),
            };
            var patternRect = new SvgRectangle()
            {
                Width = new SvgUnit(SvgUnitType.Pixel, 2),
                Height = new SvgUnit(SvgUnitType.Pixel, 2),
                Fill = new SvgColourServer(Color.Gold),
                Opacity = 0.75f
            };
            pattern.Children.Add(patternRect);
            def.Children.Add(pattern);
            group.Children.Add(def);

            var xGridWidth = (float)XGridCalculator.CalculateXUnitSize(ctx.Option.XGridDisplayMaxUnit, ctx.Option.ViewWidth, 1);

            void AppendBeam(BeamStart start)
            {
                var polyline = new SvgPolyline();
                polyline.AddCustomClass("beamPolyline");
                var collection = new SvgPointCollection();
                var width = xGridWidth * 3f * start.WidthId;

                polyline.StrokeWidth = new SvgUnit(SvgUnitType.Pixel, width);
                polyline.CustomAttributes.Add("stroke", "url(#beamBody)");
                polyline.StrokeLineCap = SvgStrokeLineCap.Round;
                polyline.Fill = new SvgColourServer(Color.Transparent);

                foreach (var pos in start.GenAllPath().Select(x => x.pos).Select(point =>
                {
                    var x = (float)ctx.CalculateToX(point.X * 1.0 / XGrid.DEFAULT_RES_X);
                    var y = (float)ctx.CalculateToY(point.Y * 1.0 / TGrid.DEFAULT_RES_T);

                    return new PointF(x, y);
                }))
                {
                    collection.Add(new SvgUnit(SvgUnitType.Pixel, pos.X));
                    collection.Add(new SvgUnit(SvgUnitType.Pixel, pos.Y));
                }
                polyline.Points = collection;

                group.Children.Add(polyline);
            }

            foreach (var start in ctx.Fumen.Beams)
                AppendBeam(start);

            ctx.Document.Children.Add(group);
        }

        private Task<string> LoadPngAsBase64ImageFromResourceLocal(string localResourceName)
        {
            using var fs = typeof(DefaultPreviewSvgGenerator).Assembly.GetManifestResourceStream("OngekiFumenEditor.Modules.PreviewSvgGenerator.Resources." + localResourceName);
            return LoadPngAsBase64Image(fs);
        }

        private async Task<string> LoadPngAsBase64Image(Stream image)
        {
            using var ms = new MemoryStream();
            await image.CopyToAsync(ms);
            var base64Image = "data:image/png;base64," + Base64.Encode(ms.ToArray());
            return base64Image;
        }

        private async Task SerializeTap(GenerateContext ctx)
        {
            var tapGroup = new SvgGroup();
            tapGroup.AddCustomClass("tapGroup");
            var exTapGroup = new SvgGroup();
            exTapGroup.AddCustomClass("exTapGroup");
            var flickGroup = new SvgGroup();
            flickGroup.AddCustomClass("flickGroup");
            var exFlickGroup = new SvgGroup();
            exFlickGroup.AddCustomClass("exflickGroup");
            exTapGroup.Filter = new Uri("url(#criticalEffect)", UriKind.Relative);
            //exFlickGroup.Filter = new Uri("url(#criticalEffect)", UriKind.Relative);
            var holdBodyGroup = new SvgGroup();
            holdBodyGroup.AddCustomClass("holdBodyGroup");
            var def = new SvgDefinitionList();
            var map = new Dictionary<string, (PointF anchor, SizeF size)>();

            void RegisterSprite(string id, PointF anchor, SizeF size, string imgData)
            {
                var bellSprite = new SvgImage();

                bellSprite.Width = size.Width;
                bellSprite.ID = id;
                bellSprite.Height = size.Height;
                bellSprite.Href = imgData;

                def.Children.Add(bellSprite);
                map[id] = (anchor, size);
            }

            var cmnSize = new SizeF(50, 60);
            var cmnPoint = new PointF(cmnSize.Width / 2, cmnSize.Height / 2);
            RegisterSprite("red", cmnPoint, cmnSize, await LoadPngAsBase64ImageFromResourceLocal("red_tap.png"));
            RegisterSprite("green", cmnPoint, cmnSize, await LoadPngAsBase64ImageFromResourceLocal("green_tap.png"));
            RegisterSprite("blue", cmnPoint, cmnSize, await LoadPngAsBase64ImageFromResourceLocal("blue_tap.png"));
            cmnSize = new SizeF(40, 40);
            cmnPoint = new PointF(cmnSize.Width / 2, cmnSize.Height / 2);
            RegisterSprite("walltap", cmnPoint, cmnSize, await LoadPngAsBase64ImageFromResourceLocal("walltap.png"));
            RegisterSprite("walltap2", cmnPoint, cmnSize, await LoadPngAsBase64ImageFromResourceLocal("walltap2.png"));
            cmnSize = new SizeF(100, 67);
            cmnPoint = new PointF(cmnSize.Width / 2, cmnSize.Height - 10);
            RegisterSprite("flick", cmnPoint, cmnSize, await LoadPngAsBase64ImageFromResourceLocal("flick.png"));
            RegisterSprite("flick1", cmnPoint, cmnSize, await LoadPngAsBase64ImageFromResourceLocal("flick1.png"));

            void AppendTap(LaneType? laneType, XGrid xGrid, TGrid tGrid, bool isCritical)
            {
                var svgTap = new SvgUse();
                var id = laneType switch
                {
                    LaneType.Left => "red",
                    LaneType.Center => "green",
                    LaneType.Right => "blue",
                    LaneType.WallRight => "walltap",
                    LaneType.WallLeft => "walltap2",
                    _ => default,
                };

                if (id is null)
                    return;
                svgTap.AddCustomClass($"tapImage_{laneType}");
                (var anchor, var size) = map[id];

                svgTap.X = new SvgUnit(SvgUnitType.Pixel, ctx.CalculateToX(xGrid) - anchor.X);
                svgTap.Y = new SvgUnit(SvgUnitType.Pixel, ctx.CalculateToY(tGrid) - anchor.Y);

                svgTap.ReferencedElement = new Uri("#" + id, UriKind.Relative);

                (isCritical ? exTapGroup : tapGroup).Children.Add(svgTap);
            }

            void AppendFlick(XGrid xGrid, TGrid tGrid, bool isCritical, bool isRight)
            {
                var svgBell = new SvgUse();
                svgBell.AddCustomClass($"flickImage_{(isRight ? "Right" : "Left")}");
                var id = isRight ? "flick1" : "flick";

                if (id is null)
                    return;

                (var anchor, var size) = map[id];

                svgBell.X = new SvgUnit(SvgUnitType.Pixel, ctx.CalculateToX(xGrid) - anchor.X);
                svgBell.Y = new SvgUnit(SvgUnitType.Pixel, ctx.CalculateToY(tGrid) - anchor.Y);

                svgBell.ReferencedElement = new Uri("#" + id, UriKind.Relative);

                flickGroup.Children.Add(svgBell);

                if (isCritical)
                {
                    var flickExEff = new SvgRectangle();
                    flickExEff.AddCustomClass($"exflickRect_{(isRight ? "Right" : "Left")}");
                    flickExEff.Width = 96;
                    flickExEff.Height = 12;
                    flickExEff.X = new SvgUnit(SvgUnitType.Pixel, ctx.CalculateToX(xGrid) - flickExEff.Width / 2);
                    if (!isRight)
                        flickExEff.X += +2;
                    flickExEff.Y = new SvgUnit(SvgUnitType.Pixel, ctx.CalculateToY(tGrid) - flickExEff.Height / 2);
                    flickExEff.Stroke = new SvgColourServer(Color.Gold);
                    flickExEff.Fill = new SvgColourServer(Color.Transparent);
                    flickExEff.StrokeWidth = new SvgUnit(SvgUnitType.Pixel, 4);

                    exFlickGroup.Children.Add(flickExEff);
                }
            }

            var wallLeftColor = Color.FromArgb(255, 34, 4, 117);
            var wallRightColor = Color.FromArgb(255, 136, 3, 152);

            void AppendHoldBody(Hold hold)
            {
                var holdEnd = hold.HoldEnd;
                var path = new SvgPolyline();
                path.AddCustomClass("holdPolyline");
                var color = hold.ReferenceLaneStart?.LaneType switch
                {
                    LaneType.Left => Color.Red,
                    LaneType.Center => Color.Green,
                    LaneType.Right => Color.Blue,
                    LaneType.WallLeft => wallLeftColor,
                    LaneType.WallRight => wallRightColor,
                    _ => default,
                };
                path.Stroke = new SvgColourServer(color);
                path.Fill = SvgColourServer.None;
                path.Opacity = 0.90f;
                path.StrokeWidth = 15;
                path.StrokeLineJoin = SvgStrokeLineJoin.Round;
                path.StrokeLineCap = SvgStrokeLineCap.Butt;

                PointF PostPoint2(double tGridUnit, double xGridUnit)
                {
                    var x = ctx.CalculateToX(xGridUnit);
                    var y = ctx.CalculateToY(tGridUnit);

                    return new(x, y);
                }

                var holdPoint = PostPoint2(hold.TGrid.TotalUnit, hold.XGrid.TotalUnit);
                var holdEndPoint = PostPoint2(holdEnd.TGrid.TotalUnit, holdEnd.XGrid.TotalUnit);

                bool checkDiscardByHorizon(PointF prev, PointF end, PointF cur)
                {
                    if (prev.Y == cur.Y && end.Y == cur.Y)
                    {
                        if (cur.X < MathF.Min(prev.X, end.X) || cur.X > MathF.Max(prev.X, end.X))
                            return true;
                    }
                    return false;
                }

                using var d = ObjectPool<List<PointF>>.GetWithUsingDisposable(out var list, out _);
                list.Clear();
                QueryVisibleLineVertices(ctx, hold.ReferenceLaneStart, hold.TGrid, hold.EndTGrid, list);
                if (list.Count > 0)
                {
                    while (list.Count > 0 && holdPoint.Y < list[0].Y)
                        list.RemoveAt(0);
                    if (list.Count >= 2)
                    {
                        var outSide = list[0];
                        var inSide = list[1];

                        if (checkDiscardByHorizon(inSide, holdPoint, outSide))
                            list.RemoveAt(0);
                    }
                    list.Insert(0, holdPoint);
                    while (list.Count > 0 && holdEndPoint.Y > list[list.Count - 1].Y)
                        list.RemoveAt(list.Count - 1);
                    if (list.Count >= 2)
                    {
                        var outSide = list[list.Count - 1];
                        var inSide = list[list.Count - 2];

                        if (checkDiscardByHorizon(inSide, holdEndPoint, outSide))
                            list.RemoveAt(list.Count - 1);
                    }
                    list.Add(holdEndPoint);
                }
                else
                {
                    list.Add(holdPoint);
                    list.Add(holdEndPoint);
                }

                var collection = new SvgPointCollection();
                foreach (var point in list)
                {
                    collection.Add(new SvgUnit(SvgUnitType.Pixel, point.X));
                    collection.Add(new SvgUnit(SvgUnitType.Pixel, point.Y));
                }

                path.Points = collection;
                holdBodyGroup.Children.Add(path);
            }

            foreach (var tap in ctx.Fumen.Taps.OfType<Tap>())
                AppendTap(tap.ReferenceLaneStart?.LaneType, tap.XGrid, tap.TGrid, tap.IsCritical);
            foreach (var hold in ctx.Fumen.Holds.Where(x => x.HoldEnd is not null))
            {
                AppendHoldBody(hold);
                AppendTap(hold.ReferenceLaneStart?.LaneType, hold.XGrid, hold.TGrid, hold.IsCritical);
                AppendTap(hold.ReferenceLaneStart?.LaneType, hold.HoldEnd.XGrid, hold.HoldEnd.TGrid, false);
            }
            foreach (var flick in ctx.Fumen.Flicks)
                AppendFlick(flick.XGrid, flick.TGrid, flick.IsCritical, flick.Direction == Flick.FlickDirection.Right);

            ctx.Document.Children.Add(def);
            ctx.Document.Children.Add(holdBodyGroup);
            ctx.Document.Children.Add(tapGroup);
            ctx.Document.Children.Add(exTapGroup);
            ctx.Document.Children.Add(exFlickGroup);
            ctx.Document.Children.Add(flickGroup);
        }

        private void QueryVisibleLineVertices(GenerateContext ctx, LaneStartBase start, TGrid min, TGrid max, List<PointF> outVertices)
        {
            if (start is null)
                return;

            var resT = start.TGrid.ResT;
            var resX = start.XGrid.ResX;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void PostPoint2(double tGridUnit, double xGridUnit)
            {
                var x = (float)ctx.CalculateToX(xGridUnit);
                var y = (float)ctx.CalculateToY(tGridUnit);

                outVertices.Add(new(x, y));
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void PostPoint(TGrid tGrid, XGrid xGrid) => PostPoint2(tGrid.TotalUnit, xGrid.TotalUnit);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void PostObject(OngekiMovableObjectBase obj) => PostPoint(obj.TGrid, obj.XGrid);

            bool CheckVisible(TGrid tGrid)
                => min <= tGrid && tGrid <= max;

            var prevVisible = CheckVisible(start.TGrid);
            var alwaysDrawing = false;

            PostObject(start);
            var prevInvaild = true;
            var prevObj = start as ConnectableObjectBase;

            foreach (var childObj in start.Children)
            {
                var visible = alwaysDrawing || CheckVisible(childObj.TGrid);
                var curIsVaild = childObj.IsVaildPath;
                if (prevInvaild != curIsVaild)
                {
                    PostObject(prevObj);
                    prevInvaild = curIsVaild;
                }

                if (prevVisible != visible && prevVisible == false)
                    PostObject(prevObj);

                if (visible || prevVisible)
                {
                    if (childObj.IsCurvePath)
                    {
                        foreach (var item in childObj.GetConnectionPaths())
                            PostPoint2(item.pos.Y / resT, item.pos.X / resX);
                    }
                    else
                        PostObject(childObj);
                }

                prevObj = childObj;
                prevVisible = visible;
            }
        }

        private async Task SerializeBell(GenerateContext ctx)
        {
            var bellGroup = new SvgGroup();
            bellGroup.AddCustomClass("bellGroup");
            var bulletGroup = new SvgGroup();
            bulletGroup.AddCustomClass("bulletGroup");

            var def = new SvgDefinitionList();

            var map = new Dictionary<string, (PointF anchor, SizeF size)>();

            void RegisterSprite(string id, PointF anchor, SizeF size, string imgData)
            {
                var bellSprite = new SvgImage();

                bellSprite.Width = size.Width;
                bellSprite.ID = id;
                bellSprite.Height = size.Height;
                bellSprite.Href = imgData;

                def.Children.Add(bellSprite);
                map[id] = (anchor, size);
            }

            var cmnPoint = new PointF(20, 20);
            var cmnSize = new SizeF(40, 40);
            RegisterSprite("bellImg", cmnPoint, cmnSize, await LoadPngAsBase64ImageFromResourceLocal("bell.png"));

            RegisterSprite($"bulletImg_{BulletType.Circle}_{BulletDamageType.Normal}", cmnPoint, cmnSize, await LoadPngAsBase64ImageFromResourceLocal("nt_mine_red.png"));
            RegisterSprite($"bulletImg_{BulletType.Circle}_{BulletDamageType.Hard}", cmnPoint, cmnSize, await LoadPngAsBase64ImageFromResourceLocal("nt_mine_pur.png"));
            RegisterSprite($"bulletImg_{BulletType.Circle}_{BulletDamageType.Danger}", cmnPoint, cmnSize, await LoadPngAsBase64ImageFromResourceLocal("nt_mine_blk.png"));

            cmnPoint = new PointF(20, 94);
            cmnSize = new SizeF(40, 100);
            RegisterSprite($"bulletImg_{BulletType.Square}_{BulletDamageType.Normal}", cmnPoint, cmnSize, await LoadPngAsBase64ImageFromResourceLocal("tri_bullet0.png"));
            RegisterSprite($"bulletImg_{BulletType.Square}_{BulletDamageType.Hard}", cmnPoint, cmnSize, await LoadPngAsBase64ImageFromResourceLocal("tri_bullet1.png"));
            RegisterSprite($"bulletImg_{BulletType.Square}_{BulletDamageType.Danger}", cmnPoint, cmnSize, await LoadPngAsBase64ImageFromResourceLocal("tri_bullet2.png"));

            RegisterSprite($"bulletImg_{BulletType.Needle}_{BulletDamageType.Normal}", cmnPoint, cmnSize, await LoadPngAsBase64ImageFromResourceLocal("sqrt_bullet0.png"));
            RegisterSprite($"bulletImg_{BulletType.Needle}_{BulletDamageType.Hard}", cmnPoint, cmnSize, await LoadPngAsBase64ImageFromResourceLocal("sqrt_bullet1.png"));
            RegisterSprite($"bulletImg_{BulletType.Needle}_{BulletDamageType.Danger}", cmnPoint, cmnSize, await LoadPngAsBase64ImageFromResourceLocal("sqrt_bullet2.png"));

            string GetDefId(IBulletPalleteReferencable referencable)
            {
                if (referencable is Bell)
                    return "bellImg";
                if (referencable is Bullet blt)
                    return $"bulletImg_{blt.TypeValue}_{blt.BulletDamageTypeValue}";
                return null;
            }

            void AppendBell(SvgGroup group, IBulletPalleteReferencable referencable)
            {
                var svgBell = new SvgUse();
                var id = GetDefId(referencable);
                if (id is null)
                    return;
                svgBell.AddCustomClass(id);

                (var anchor, var size) = map[id];

                svgBell.X = new SvgUnit(SvgUnitType.Pixel, ctx.CalculateToX(referencable.XGrid) - anchor.X);
                svgBell.Y = new SvgUnit(SvgUnitType.Pixel, ctx.CalculateToY(referencable.TGrid) - anchor.Y);

                svgBell.ReferencedElement = new Uri("#" + id, UriKind.Relative);

                group.Children.Add(svgBell);
            }

            foreach (var bell in ctx.Fumen.Bells)
                AppendBell(bellGroup, bell);
            foreach (var bell in ctx.Fumen.Bullets)
                AppendBell(bulletGroup, bell);

            ctx.Document.Children.Add(def);
            ctx.Document.Children.Add(bellGroup);
            ctx.Document.Children.Add(bulletGroup);
        }

        private async Task SerializeEvents(GenerateContext ctx)
        {
            var group = new SvgGroup();
            group.AddCustomClass("eventGroup");
            var def = new SvgDefinitionList();
            var gradient = new SvgLinearGradientServer() { ID = "timelineStroke" };
            gradient.Children.Add(new SvgGradientStop()
            {
                Offset = new SvgUnit(SvgUnitType.Percentage, 0),
                StopColor = new SvgColourServer(Color.WhiteSmoke)
            });
            gradient.Children.Add(new SvgGradientStop()
            {
                Offset = new SvgUnit(SvgUnitType.Percentage, 20),
                StopColor = new SvgColourServer(Color.WhiteSmoke)
            });
            gradient.Children.Add(new SvgGradientStop()
            {
                Offset = new SvgUnit(SvgUnitType.Percentage, 25),
                StopColor = new SvgColourServer(Color.Transparent)
            });
            gradient.Children.Add(new SvgGradientStop()
            {
                Offset = new SvgUnit(SvgUnitType.Percentage, 75),
                StopColor = new SvgColourServer(Color.Transparent)
            });
            gradient.Children.Add(new SvgGradientStop()
            {
                Offset = new SvgUnit(SvgUnitType.Percentage, 80),
                StopColor = new SvgColourServer(Color.WhiteSmoke)
            });
            gradient.Children.Add(new SvgGradientStop()
            {
                Offset = new SvgUnit(SvgUnitType.Percentage, 100),
                StopColor = new SvgColourServer(Color.WhiteSmoke)
            });
            def.Children.Add(gradient);

            void AppendTimeline(string text, float y)
            {
                y = (float)(ctx.TotalHeight - y);
                var apGroup = new SvgGroup();
                apGroup.AddCustomClass("timelineGroup");

                var svgText = new SvgText
                {
                    Text = text,
                    Fill = new SvgColourServer(Color.WhiteSmoke)
                };
                svgText.AddCustomClass("timelineText");
                svgText.Y.Add(new SvgUnit(SvgUnitType.Pixel, y - 5));

                var svgLine = new SvgLine
                {
                    StartX = new SvgUnit(SvgUnitType.Percentage, 0f),
                    EndX = new SvgUnit(SvgUnitType.Percentage, 100f),
                    StrokeWidth = 1
                };
                svgLine.AddCustomClass("timelineLine");
                svgLine.StartY = svgLine.EndY = new SvgUnit(SvgUnitType.Pixel, y);

                svgText.TextAnchor = SvgTextAnchor.End;
                svgText.X.Add(new SvgUnit(SvgUnitType.Percentage, 100f));
                svgLine.CustomAttributes.Add("stroke", "url(#timelineStroke)");
                svgLine.EndY = new SvgUnit(SvgUnitType.Pixel, y + 0.1f);

                apGroup.Children.Add(svgText);
                apGroup.Children.Add(svgLine);

                group.Children.Add(apGroup);
            }

            var timelines = TGridCalculator.GetVisbleTimelines_DesignMode(
                    ctx.Fumen.Soflans,
                    ctx.Fumen.BpmList,
                    ctx.Fumen.MeterChanges,
                    0,
                    ctx.TotalHeight,
                    0,
                    1,
                    ctx.Option.VerticalScale)
                .Where(x => x.beatIndex == 0);

            foreach (var timeline in timelines)
                AppendTimeline($"T[{timeline.tGrid.Unit},{timeline.tGrid.Grid}]", (float)timeline.y);

            var objs = Enumerable.Empty<ITimelineObject>()
                .Concat(ctx.Fumen.MeterChanges)
                .Concat(ctx.Fumen.BpmList)
                .Concat(ctx.Fumen.EnemySets)
                .Concat(ctx.Fumen.Soflans.GenerateKeyframeSoflans(ctx.Fumen.BpmList))
                .GroupBy(x => x.TGrid)
                .OrderBy(x => x.Key);

            (SvgElement gen, float width) GeneateOffsetedItem(string content, Color color, float offset, float y)
            {
                var svgText = new SvgText();
                svgText.AddCustomClass("eventText");
                svgText.Text = content;
                svgText.Y.Add(new SvgUnit(SvgUnitType.Pixel, y - 5));
                svgText.Fill = new SvgColourServer(color);
                svgText.TextAnchor = SvgTextAnchor.Start;
                svgText.X.Add(new SvgUnit(SvgUnitType.Pixel, 5 + offset));

                var bound = svgText.Bounds;
                return (svgText, Math.Max(0, bound.Width + 40));
            }

            var prevSpeed = 1f;
            foreach (var groupItems in objs)
            {
                var items = groupItems.OrderBy(x => x.GetType().Name).ToArray();
                var apGroup = new SvgGroup();
                apGroup.AddCustomClass("eventGroup");
                var offsetX = 0f;
                var y = ctx.CalculateToY(groupItems.Key);

                foreach (var obj in items)
                {
                    (var content, var color) = obj switch
                    {
                        IKeyframeSoflan sfl => ($"{sfl.Speed:F2}x", Color.Cyan),
                        EnemySet set => ($"{set.TagTblValue}", Color.Yellow),
                        BPMChange bpm => ($"♫ {bpm.BPM:f2}", Color.LightPink),
                        MeterChange met => ($"{met.BunShi} / {met.Bunbo}", Color.LightGreen),
                    };

                    if (obj is IKeyframeSoflan spd)
                    {
                        if (prevSpeed != spd.Speed)
                        {
                            content += " " + (spd.Speed > prevSpeed ? "+" : "-");
                            prevSpeed = spd.Speed;
                        }
                    }

                    (var gen, var width) = GeneateOffsetedItem(content, color, offsetX, y);
                    apGroup.Children.Add(gen);
                    var newOffsetX = offsetX + width;

                    //画个线
                    var svgLine = new SvgLine();
                    svgLine.AddCustomClass("eventLine");
                    svgLine.StartX = new SvgUnit(SvgUnitType.Pixel, offsetX);
                    svgLine.EndX = new SvgUnit(SvgUnitType.Pixel, newOffsetX);
                    svgLine.StartY = svgLine.EndY = new SvgUnit(SvgUnitType.Pixel, y);
                    svgLine.StrokeWidth = 1;
                    svgLine.Stroke = new SvgColourServer(color);
                    apGroup.Children.Add(svgLine);

                    //更新offsetX
                    offsetX = newOffsetX;
                }

                group.Children.Add(apGroup);
            }

            ctx.Document.Children.Add(def);
            ctx.Document.Children.Add(group);
        }

        private async Task SerializeLanes(GenerateContext ctx)
        {
            SvgElement GeneratePattern(System.Windows.Media.Color laneColor)
            {
                var pattern = new SvgPatternServer()
                {
                    Height = new SvgUnit(SvgUnitType.Pixel, 10),
                    Width = new SvgUnit(SvgUnitType.Pixel, 10),
                    X = new SvgUnit(SvgUnitType.Pixel, 0),
                    Y = new SvgUnit(SvgUnitType.Pixel, 0),
                    PatternUnits = SvgCoordinateUnits.UserSpaceOnUse
                };
                pattern.Children.Add(new SvgLine()
                {
                    StartX = new SvgUnit(SvgUnitType.Pixel, 0),
                    StartY = new SvgUnit(SvgUnitType.Pixel, 0),
                    EndX = new SvgUnit(SvgUnitType.Pixel, 11),
                    EndY = new SvgUnit(SvgUnitType.Pixel, 11),
                    Stroke = new SvgColourServer(Color.FromArgb(laneColor.A, laneColor.R, laneColor.G, laneColor.B)),
                    Opacity = 0.75f,
                    StrokeWidth = 2
                });
                return pattern;
            }

            var def = new SvgDefinitionList();

            var leftPattern = GeneratePattern(System.Windows.Media.Colors.WhiteSmoke/*LaneColor.AllLaneColors.FirstOrDefault(x => x.LaneType == LaneType.WallLeft).Color*/);
            leftPattern.ID = "leftLBKEffect";
            def.Children.Add(leftPattern);

            var rightPattern = GeneratePattern(System.Windows.Media.Colors.WhiteSmoke/*LaneColor.AllLaneColors.FirstOrDefault(x => x.LaneType == LaneType.WallRight).Color*/);
            rightPattern.ID = "rightLBKEffect";
            def.Children.Add(rightPattern);

            var laneGroup = new SvgGroup();
            laneGroup.AddCustomClass("laneGroup");
            var lbkGroup = new SvgGroup() { ID = "lbk" };
            lbkGroup.AddCustomClass("lbkGroup");
            lbkGroup.Children.Add(def);

            void AppendLane(LaneStartBase start, int width)
            {
                var polyline = new SvgPolyline();
                polyline.AddCustomClass($"lanePolyline_{start.LaneType}");
                var collection = new SvgPointCollection();

                var pathPointItor = start.GenAllPath().Select(x => x.pos).Select(point =>
                {
                    var x = (float)ctx.CalculateToX(point.X * 1.0 / XGrid.DEFAULT_RES_X);
                    var y = (float)ctx.CalculateToY(point.Y * 1.0 / TGrid.DEFAULT_RES_T);

                    return new PointF(x, y);
                }).GetEnumerator();

                if (pathPointItor.MoveNext())
                {
                    var first = pathPointItor.Current;
                    collection.Add(new(SvgUnitType.Pixel, first.X));
                    collection.Add(new(SvgUnitType.Pixel, first.Y));
                    while (pathPointItor.MoveNext())
                    {
                        var next = pathPointItor.Current;
                        collection.Add(new(SvgUnitType.Pixel, next.X));
                        collection.Add(new(SvgUnitType.Pixel, next.Y));
                    }

                    var color = LaneColor.AllLaneColors.FirstOrDefault(x => x.LaneType == start.LaneType).Color;
                    if (start is IColorfulLane colorfulLane)
                        color = colorfulLane.ColorId.Color;

                    polyline.Points = collection;
                    polyline.Fill = SvgColourServer.None;
                    polyline.Stroke = new SvgColourServer(Color.FromArgb(color.A, color.R, color.G, color.B));
                    polyline.StrokeWidth = width;

                    laneGroup.Children.Add(polyline);
                }
            }

            var overdrawingDefferSet = new HashSet<int>();

            void AppendLBK(LaneBlockArea lbk)
            {
                var hashCode = lbk.GetHashCode();
                if (overdrawingDefferSet.Contains(hashCode))
                    return;
                else
                    overdrawingDefferSet.Add(hashCode);

                var offsetX = (lbk.Direction == LaneBlockArea.BlockDirection.Left ? -1 : 1) * 60;
                var id = lbk.Direction == LaneBlockArea.BlockDirection.Left ? "leftLBKEffect" : "rightLBKEffect";
                var fumen = ctx.Fumen;
                /*
                var laneColor = (lbk.Direction == LaneBlockArea.BlockDirection.Left ?
                    LaneColor.AllLaneColors.FirstOrDefault(x => x.LaneType == LaneType.WallLeft) :
                    LaneColor.AllLaneColors.FirstOrDefault(x => x.LaneType == LaneType.WallRight)).Color;
                var color = Color.FromArgb(laneColor.A, laneColor.R, laneColor.G, laneColor.B);
                */
                var color = Color.WhiteSmoke;
                #region Generate LBK lines

                void BuildLBK(IEnumerable<Vector2> gridPoints)
                {
                    var points = gridPoints.Select(x =>
                    {
                        var xUnit = x.X / XGrid.DEFAULT_RES_X;
                        var tUnit = x.Y / TGrid.DEFAULT_RES_T;

                        return new PointF(ctx.CalculateToX(xUnit), ctx.CalculateToY(tUnit));
                    }).ToArray();

                    var outputPoints = new SvgPointCollection();

                    foreach (var point in points)
                    {
                        outputPoints.Add(new SvgUnit(SvgUnitType.Pixel, point.X));
                        outputPoints.Add(new SvgUnit(SvgUnitType.Pixel, point.Y));
                    }

                    foreach (var point in points.Reverse())
                    {
                        outputPoints.Add(new SvgUnit(SvgUnitType.Pixel, point.X + offsetX));
                        outputPoints.Add(new SvgUnit(SvgUnitType.Pixel, point.Y));
                    }

                    outputPoints.Add(new SvgUnit(SvgUnitType.Pixel, points[0].X));
                    outputPoints.Add(new SvgUnit(SvgUnitType.Pixel, points[0].Y));

                    //todo gen lbk here
                    var polyline = new SvgPolygon()
                    {
                        Points = outputPoints,
                        Stroke = new SvgColourServer(color),
                        StrokeWidth = 1
                    };
                    polyline.AddCustomClass($"lbkPolyline_{lbk.Direction}");
                    polyline.CustomAttributes.Add("fill", $"url(#{id})");
                    lbkGroup.Children.Add(polyline);
                }

                void PostPointByTGrid(ConnectableChildObjectBase obj, TGrid fromTGrid, TGrid toTGrid, List<Vector2> list)
                {
                    var fromXGridOpt = obj.CalulateXGridTotalGrid(fromTGrid.TotalGrid);
                    var toXGridOpt = obj.CalulateXGridTotalGrid(toTGrid.TotalGrid);
                    if (fromXGridOpt is double fromXGrid && toXGridOpt is double toXGrid)
                    {
                        list.Add(new((float)fromXGrid, fromTGrid.TotalGrid));
                        list.Add(new((float)toXGrid, toTGrid.TotalGrid));
                    }
                }

                void ProcessConnectable(ConnectableChildObjectBase obj, TGrid minTGrid, TGrid maxTGrid, List<Vector2> list)
                {
                    var minTotalGrid = minTGrid.TotalGrid;
                    var maxTotalGrid = maxTGrid.TotalGrid;

                    if (!obj.IsCurvePath)
                    {
                        //直线，优化
                        PostPointByTGrid(obj, minTGrid, maxTGrid, list);
                    }
                    else
                    {
                        foreach ((var gridVec2, var isVaild) in obj.GetConnectionPaths().Where(x => x.pos.Y <= maxTotalGrid && x.pos.Y >= minTotalGrid))
                            list.Add(new(gridVec2.X, gridVec2.Y));
                    }
                }

                void ProcessWallLane(LaneStartBase wallStartLane, TGrid minTGrid, TGrid maxTGrid)
                {
                    using var d = ObjectPool<List<Vector2>>.GetWithUsingDisposable(out var list, out _);
                    list.Clear();

                    foreach (var child in wallStartLane.Children)
                    {
                        if (child.TGrid < minTGrid)
                            continue;
                        if (child.PrevObject.TGrid > maxTGrid)
                            break;

                        var childMinTGrid = MathUtils.Max(minTGrid, child.PrevObject.TGrid);
                        var childMaxTGrid = MathUtils.Min(maxTGrid, child.TGrid);

                        ProcessConnectable(child, childMinTGrid, childMaxTGrid, list);
                    }

                    BuildLBK(list);
                }

                #endregion

                var itor = lbk
                .GetAffactableWallLanes(fumen)
                .OrderBy(x => x.TGrid)
                .GetEnumerator();

                var beginTGrid = lbk.TGrid;
                var endTGrid = lbk.EndIndicator.TGrid;

                if (itor.MoveNext())
                {
                    ProcessWallLane(itor.Current, beginTGrid, endTGrid);

                    while (itor.MoveNext())
                    {
                        var start = itor.Current;

                        ProcessWallLane(start, beginTGrid, endTGrid);
                    }
                }
            }

            foreach (var laneStart in ctx.Fumen.Lanes)
                AppendLane(laneStart, laneStart.IsWallLane ? 4 : 2);
            foreach (var lbk in ctx.Fumen.LaneBlocks)
                AppendLBK(lbk);

            ctx.Document.Children.Add(lbkGroup);
            ctx.Document.Children.Add(laneGroup);
        }

        private async Task SerializePlayField(GenerateContext ctx)
        {
            var group = new SvgGroup();
            group.AddCustomClass("playfieldGroup");

            const long defaultLeftX = -24 * XGrid.DEFAULT_RES_X;
            const long defaultRightX = 24 * XGrid.DEFAULT_RES_X;

            var backgroundPolygon = new SvgPolygon()
            {
                Points = new SvgPointCollection()
                {
                    new(SvgUnitType.Pixel, 0), new(SvgUnitType.Pixel, 0),
                    new(SvgUnitType.Pixel, (float)ctx.TotalHeight), new(SvgUnitType.Pixel, 0),
                    new(SvgUnitType.Pixel, (float)ctx.TotalHeight), new(SvgUnitType.Pixel, (float)ctx.TotalHeight),
                    new(SvgUnitType.Pixel, 0), new(SvgUnitType.Pixel, (float)ctx.TotalHeight),
                },
                Fill = new SvgColourServer(Color.DarkCyan),
                FillOpacity = 0.85f
            };
            backgroundPolygon.AddCustomClass("playfieldBackground");
            group.Children.Add(backgroundPolygon);

            List<Vector2> EnumeratePoints(bool isRight)
            {
                var defaultX = isRight ? defaultRightX : defaultLeftX;
                var type = isRight ? LaneType.WallRight : LaneType.WallLeft;
                var ranges = CombinableRange<int>.CombineRanges(ctx.Fumen.Lanes
                    .Where(x => x.LaneType == type)
                    .Select(x => new CombinableRange<int>(x.MinTGrid.TotalGrid, x.MaxTGrid.TotalGrid)))
                    .OrderBy(x => isRight ? x.Max : x.Min).ToArray();

                var result = new List<Vector2>();
                var points = new HashSet<float>();

                void appendPoint(List<Vector2> list, XGrid xGrid, float y)
                {
                    if (xGrid is null)
                        return;
                    list.Add(new(xGrid.TotalGrid, y));
                }

                for (int i = 0; i < ranges.Length; i++)
                {
                    var curRange = ranges[i];
                    var nextRange = ranges.ElementAtOrDefault(i + 1);

                    var lanes = ctx.Fumen.Lanes
                        .GetVisibleStartObjects(TGrid.FromTotalGrid(curRange.Min), TGrid.FromTotalGrid(curRange.Max))
                        .Where(x => x.LaneType == type)
                        .ToArray();

                    var polylines = lanes.Select(x => x.GenAllPath().Select(x => x.pos).SequenceConsecutivelyWrap(2).Select(x => (x.FirstOrDefault(), x.LastOrDefault())).ToArray())
                        .ToArray();

                    for (int r = 0; r < polylines.Length; r++)
                    {
                        var polylineA = polylines[r];
                        for (int t = r + 1; t < polylines.Length; t++)
                        {
                            var polylineB = polylines[t];

                            for (int ai = 0; ai < polylineA.Length; ai++)
                            {
                                for (int bi = 0; bi < polylineB.Length; bi++)
                                {
                                    var a = polylineA[ai];
                                    var b = polylineB[bi];

                                    if (a == b)
                                        continue;

                                    if (GetLinesIntersection(
                                        a.Item1.ToSystemNumericsVector2(),
                                        a.Item2.ToSystemNumericsVector2(),
                                        b.Item1.ToSystemNumericsVector2(),
                                        b.Item2.ToSystemNumericsVector2()) is System.Numerics.Vector2 p)
                                        points.Add((float)p.Y);
                                }
                            }
                        }
                    }

                    points.AddRange(lanes.Select(x => (float)x.TGrid.TotalGrid).Concat(lanes.Select(x => x.Children.LastOrDefault()).FilterNull().Select(x => (float)x.TGrid.TotalGrid)));
                }

                var idx = 0;
                var sortedPoints = points.OrderBy(x => x).ToList();
                if (sortedPoints.IsEmpty() || sortedPoints.FirstOrDefault() > 0)
                    sortedPoints.Insert(0, 0);
                if (sortedPoints.LastOrDefault() < ctx.MaxTGrid.TotalGrid)
                    sortedPoints.Add(ctx.MaxTGrid.TotalGrid);

                var segments = sortedPoints.SequenceConsecutivelyWrap(2).Select(x => (x.FirstOrDefault(), x.LastOrDefault())).ToArray();

                foreach ((var fromY, var toY) in segments)
                {
                    var midY = ((fromY + toY) / 2);
                    var midTGrid = TGrid.FromTotalGrid((int)midY);

                    var pickables = ctx.Fumen.Lanes
                            .GetVisibleStartObjects(midTGrid, midTGrid)
                            .Where(x => x.LaneType == type)
                            .Select(x => (x.CalulateXGrid(midTGrid), x))
                            .FilterNullBy(x => x.Item1)
                            .ToArray();

                    (var midXGrid, var pickLane) = pickables.IsEmpty() ? default : (isRight ? pickables.MaxBy(x => x.Item1) : pickables.MinBy(x => x.Item1));
                    if (pickLane is not null)
                    {
                        var fromTGrid = TGrid.FromTotalGrid((int)fromY);
                        appendPoint(result, pickLane.CalulateXGrid(fromTGrid), fromY);

                        foreach (var pos in pickLane.GenAllPath().Select(x => x.pos).SkipWhile(x => x.Y < fromY).TakeWhile(x => x.Y < toY))
                            result.Add(pos);

                        var toTGrid = TGrid.FromTotalGrid((int)toY);
                        appendPoint(result, pickLane.CalulateXGrid(toTGrid), toY);
                    }
                    else
                    {
                        //默认24咯
                        result.Add(new(defaultX, fromY));
                        result.Add(new(defaultX, toY));
                    }
                    idx++;
                }

                return result;
            }

            var leftPoints = EnumeratePoints(false);
            var rightPoints = EnumeratePoints(true);

            var leftResult = new List<(SvgUnit, SvgUnit)>();
            var rightResult = new List<(SvgUnit, SvgUnit)>();

            var collection = new SvgPointCollection();

            foreach (var point in leftPoints.Concat(rightPoints.AsEnumerable().Reverse()))
            {
                var uX = new SvgUnit(SvgUnitType.Pixel, ctx.CalculateToX(point.X / XGrid.DEFAULT_RES_X));
                var uY = new SvgUnit(SvgUnitType.Pixel, ctx.CalculateToY(point.Y / TGrid.DEFAULT_RES_T));

                collection.Add(uX);
                collection.Add(uY);
            }

            var line = new SvgPolygon
            {
                Fill = new SvgColourServer(Color.Black),
                Opacity = 0.95f,
                StrokeOpacity = 1,
                FillOpacity = 0.95f,
                Stroke = new SvgColourServer(Color.WhiteSmoke),
                Points = collection
            };
            line.AddCustomClass("playfieldForeground");

            group.Children.Add(line);
            ctx.Document.Children.Add(group);
        }
    }
}
