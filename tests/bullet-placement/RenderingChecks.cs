using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Threading;
using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.Skia;
using OngekiFumenEditor.Kernel.Graphics.Skia.Base;
using OngekiFumenEditor.Modules.FumenBulletPalleteListViewer;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.BulletBell;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using SkiaSharp;

internal static class RenderingChecks
{
    private const int Width = 640;
    private const int Height = 480;

    // Compare the helper against independent, normally rendered objects at known grid coordinates.
    // The expected image is not reconstructed from the helper's command list or its candidate.
    public static void Run(Action<bool, string> check, string repositoryRoot)
    {
        var directory = Path.Combine(repositoryRoot, "tests", "mcp-e2e", ".tmp", "pr45");
        Directory.CreateDirectory(directory);
        var settings = EditorGlobalSetting.Default;
        var oldScale = settings.TextureSizeScale;
        var oldBackend = ProgramSetting.Default.SkiaRenderBackend;
        var manager = new DefaultSkiaDrawingManagerImpl();
        var documents = IoC.Get<IEditorDocumentManager>();
        var previousEditor = documents.CurrentActivatedEditor;
        var viewer = IoC.Get<IFumenBulletPalleteListViewer>();
        Window? ownedWindow = null;
        var rows = new List<(string Name, SKBitmap Ghost, SKBitmap Normal)>();
        using var editor = CreateEditor();
        using var referenceEditor = CreateEditor();
        using var otherEditor = CreateEditor();
        using var bullets = new BulletDrawingTarget();
        using var bells = new BellDrawingTarget();
        using var helper = new DrawProjectilePlacementPreviewHelper(viewer, bullets, bells);
        try
        {
            // The real manager's DEBUG texture loader requires its ordinary initialization path.
            // Neither this CPU control nor the optional DPI owner is ever shown or attached.
            if (Application.Current.MainWindow is null)
                Application.Current.MainWindow = ownedWindow = new Window { ShowActivated = false, ShowInTaskbar = false };
            ProgramSetting.Default.SkiaRenderBackend = "CPU";
            var control = manager.CreateRenderControl();
            var initialization = manager.InitializeRenderControl(control);
            while (!initialization.IsCompleted)
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            initialization.GetAwaiter().GetResult();
            check(!control.IsVisible && !Application.Current.MainWindow.IsVisible,
                "render fixture never shows its CPU control or DPI owner");
            settings.TextureSizeScale = 1;
            bullets.Initialize(manager);
            bells.Initialize(manager);
            using var offscreen = manager.CreateOffscreenToImage(new OffscreenRenderOptions
            {
                Width = Width, Height = Height, RequireMatchingViewport = true
            });
            documents.NotifyActivate(editor);
            var pallete = new BulletPallete { StrID = "R1" };
            editor.Fumen.AddObject(pallete);
            var selected = new Bullet { XGrid = new XGrid(-2), TGrid = new TGrid(2), IsSelected = true };
            editor.Fumen.AddObject(selected);
            editor.RegisterSelectableObject(selected, new Vector2(80, 80), new Vector2(20, 20));
            var hitsBefore = editor.GetHits();
            var objectsBefore = editor.Fumen.GetAllDisplayableObjects().ToArray();
            var selectionBefore = editor.SelectObjects.ToArray();

            SKBitmap Render(Action<IDrawCommandListBuilder> draw)
            {
                using var builder = manager.CreateDrawCommandListBuilder();
                builder.SetViewport(Width, Height);
                builder.SetCleanColor(Vector4.Zero);
                builder.SetCurrentViewMatrix(Matrix4x4.CreateTranslation(-Width / 2f, -Height / 2f, 0));
                builder.SetCurrentProjectionMatrix(Matrix4x4.CreateOrthographic(Width, Height, -1, 1));
                builder.SetCurrentRect(new VisibleRect(new Vector2(Width, 0), new Vector2(0, Height)));
                draw(builder);
                using var image = offscreen.RenderToImageAsync(builder.GetDrawCommandList()).GetAwaiter().GetResult();
                return SKBitmap.FromImage(((SkiaImage)image).Image);
            }

            SKBitmap Ghost(FumenVisualEditorViewModel target)
            {
                target.RefreshBatchProjectilePlacementPreview();
                return Render(builder => helper.Draw(target, builder));
            }

            SKBitmap Normal(bool isBell, BulletType type, BulletSize size, out Rect bounds)
            {
                // Deliberately no palette: no label/highlight, and type/size are independent of the request.
                OngekiMovableObjectBase obj = isBell
                    ? new Bell { XGrid = new XGrid(2), TGrid = new TGrid(1) }
                    : new Bullet { XGrid = new XGrid(2), TGrid = new TGrid(1), TypeValue = type, SizeValue = size };
                try
                {
                    var bitmap = Render(builder =>
                    {
                        if (obj is Bell bell)
                            bells.DrawBatch(referenceEditor, builder, new[] { bell });
                        else
                            bullets.DrawBatch(referenceEditor, builder, new[] { (Bullet)obj });
                    });
                    bounds = referenceEditor.GetHits()[obj];
                    return bitmap;
                }
                finally { obj.Dispose(); }
            }

            void Case(string name, bool isBell, BulletType type, BulletSize size)
            {
                editor.CancelBatchProjectilePlacement();
                pallete.TypeValue = type;
                pallete.SizeValue = size;
                editor.ToggleBatchProjectilePlacement(pallete, isBell);
                editor.OnMouseMove(Pointer(editor));
                var candidate = editor.BatchProjectilePlacementPreview;
                check(candidate is not null && (isBell ? candidate is Bell : candidate is Bullet), name + ": candidate type");
                check(candidate!.XGrid.TotalGrid == new XGrid(2).TotalGrid && candidate.TGrid.TotalGrid == new TGrid(1).TotalGrid,
                    name + ": offset pointer snaps to X[2,0], T[1,0]");
                var ghost = Ghost(editor);
                var normal = Normal(isBell, type, size, out var bounds);
                rows.Add((name, ghost, normal));
                Save(ghost, Path.Combine(directory, name + "-ghost.png"));
                Save(normal, Path.Combine(directory, name + "-normal.png"));
                AssertSprite(check, ghost, normal, name);
                AssertAnchor(check, referenceEditor, bounds, normal, isBell, type, size, name);
                check(editor.GetHits().Count == hitsBefore.Count && hitsBefore.All(x => editor.GetHits().TryGetValue(x.Key, out var r) && r == x.Value),
                    name + ": preview does not register or disturb hit/box-selection bounds");
                check(editor.SelectObjects.SequenceEqual(selectionBefore) && !candidate.IsSelected,
                    name + ": selected chart object remains selected; candidate is not selected");
                check(editor.Fumen.GetAllDisplayableObjects().SequenceEqual(objectsBefore), name + ": no chart object added by rendering");
            }

            foreach (var type in new[] { BulletType.Circle, BulletType.Needle, BulletType.Square })
                foreach (var size in new[] { BulletSize.Normal, BulletSize.Large })
                    Case(type.ToString().ToLowerInvariant() + "-" + size.ToString().ToLowerInvariant(), false, type, size);
            Case("bell", true, BulletType.Circle, BulletSize.Normal);

            settings.TextureSizeScale = 1.75;
            Case("needle-large-scale175", false, BulletType.Needle, BulletSize.Large);
            Case("bell-scale175", true, BulletType.Circle, BulletSize.Normal);
            foreach (var pair in new[] { (Base: "needle-large", Scaled: "needle-large-scale175"), (Base: "bell", Scaled: "bell-scale175") })
            {
                var before = PixelBounds(rows.Single(x => x.Name == pair.Base).Ghost);
                var after = PixelBounds(rows.Single(x => x.Name == pair.Scaled).Ghost);
                check(Math.Abs(after.Width - before.Width * 1.75) <= 4 && Math.Abs(after.Height - before.Height * 1.75) <= 4,
                    pair.Scaled + ": rasterized sprite dimensions respond to live TextureSizeScale");
            }

            editor.OnMouseMove(new Point(-1, 100));
            using (var outside = Ghost(editor))
            {
                AssertBlank(check, outside, "outside viewport hides previous ghost");
                Save(outside, Path.Combine(directory, "outside-blank.png"));
            }
            check(editor.IsRequestPlaceBatchProjectilePreview, "outside viewport retains request");
            editor.OnMouseMove(Pointer(editor));
            using (var returned = Ghost(editor))
                AssertSamePixels(check, returned, rows.Last().Ghost, "returning pointer restores current bell, not stale bullet");

            editor.CancelBatchProjectilePlacement();
            using (var cancelled = Ghost(editor))
            {
                AssertBlank(check, cancelled, "cancel clears every ghost pixel");
                Save(cancelled, Path.Combine(directory, "cancelled-blank.png"));
            }
            editor.ToggleBatchProjectilePlacement(pallete, false);
            editor.OnMouseMove(Pointer(editor));
            documents.NotifyActivate(otherEditor);
            var otherPallete = new BulletPallete { StrID = "R2", TypeValue = BulletType.Circle };
            otherEditor.Fumen.AddObject(otherPallete);
            otherEditor.ToggleBatchProjectilePlacement(otherPallete, true);
            otherEditor.OnMouseMove(Pointer(otherEditor));
            using (var wrongEditor = Ghost(editor))
            {
                AssertBlank(check, wrongEditor, "another editor's request cannot render this editor's old candidate");
                Save(wrongEditor, Path.Combine(directory, "different-editor-blank.png"));
            }
            using (var active = Ghost(otherEditor))
            using (var expected = Normal(true, BulletType.Circle, BulletSize.Normal, out _))
                AssertSprite(check, active, expected, "new active editor renders its own bell");

            using var bulletBeforeDispose = Normal(false, BulletType.Needle, BulletSize.Large, out _);
            using var bellBeforeDispose = Normal(true, BulletType.Circle, BulletSize.Normal, out _);
            helper.Dispose();
            using var bulletAfterDispose = Normal(false, BulletType.Needle, BulletSize.Large, out _);
            using var bellAfterDispose = Normal(true, BulletType.Circle, BulletSize.Normal, out _);
            AssertSamePixels(check, bulletBeforeDispose, bulletAfterDispose, "helper Dispose leaves normal Bullet shared texture usable");
            AssertSamePixels(check, bellBeforeDispose, bellAfterDispose, "helper Dispose leaves normal Bell shared texture usable");
            Save(bulletAfterDispose, Path.Combine(directory, "after-dispose-bullet.png"));
            Save(bellAfterDispose, Path.Combine(directory, "after-dispose-bell.png"));
            SaveContactSheet(rows, directory, editor);
            Console.WriteLine("RENDER EVIDENCE: " + Path.Combine(directory, "rendering-contact-sheet.png"));
        }
        finally
        {
            editor.CancelBatchProjectilePlacement();
            otherEditor.CancelBatchProjectilePlacement();
            if (previousEditor is not null)
                documents.NotifyActivate(previousEditor);
            else if (documents.CurrentActivatedEditor is { } active)
                documents.NotifyDeactivate(active);
            settings.TextureSizeScale = oldScale;
            ProgramSetting.Default.SkiaRenderBackend = oldBackend;
            manager.Term().GetAwaiter().GetResult();
            foreach (var row in rows) { row.Ghost.Dispose(); row.Normal.Dispose(); }
            if (ownedWindow is not null)
            {
                Application.Current.MainWindow = null;
                ownedWindow.Close();
            }
        }
    }

    private static FumenVisualEditorViewModel CreateEditor()
    {
        var editor = new FumenVisualEditorViewModel
        {
            EditorProjectData = new EditorProjectDataModel { AudioDuration = TimeSpan.FromMinutes(1) },
            ViewWidth = Width,
            ViewHeight = Height
        };
        editor.Setting.VerticalDisplayScale = 1;
        editor.Setting.BeatSplit = 4;
        editor.Setting.XGridDisplayMaxUnit = 12;
        editor.Setting.XGridUnitSpace = 1;
        editor.Setting.XOffset = 0;
        editor.Setting.DisableXGridMagneticDock = true;
        editor.Setting.DisableTGridMagneticDock = false;
        editor.Setting.ForceMagneticDock = false;
        editor.Setting.ForceXGridMagneticDock = false;
        editor.Setting.ForceXGridIntegerUnit = true;
        var worldY = ((IFumenEditorDrawingContext)editor).ConvertToY_DefaultSoflanGroup(new TGrid(1));
        var origin = worldY - 220;
        editor.TotalDurationHeight = Math.Max(10000, worldY + 1000);
        editor.RectInDesignMode = new VisibleRect(new Vector2(Width, (float)origin), new Vector2(0, (float)(origin + Height)));
        editor.CurrentDrawingTargetContext = new DrawingTargetContext
        {
            ViewRelativeOriginY = origin, WorldRect = editor.RectInDesignMode,
            ViewRelativeRect = new VisibleRect(new Vector2(Width, 0), new Vector2(0, Height)),
            CurrentSoflanList = editor.Fumen.SoflansMap.DefaultSoflanList,
            ViewWidth = Width, ViewHeight = Height, RenderScaleX = 1, RenderScaleY = 1
        };
        return editor;
    }

    private static Point Pointer(FumenVisualEditorViewModel editor) =>
        new(XGridCalculator.ConvertXGridToX(new XGrid(2), editor) + 3, Height - 220 - 1);

    private static void AssertAnchor(Action<bool, string> check, FumenVisualEditorViewModel editor, Rect bounds,
        SKBitmap normal, bool isBell, BulletType type, BulletSize size, string name)
    {
        var key = isBell ? "bell" : "bullet" + type + size.ToString();
        var baseSize = isBell || type == BulletType.Circle ? new Vector2(40, 40) : new Vector2(30, 80);
        var anchor = isBell || type == BulletType.Circle ? Vector2.Zero : new Vector2(0, 35);
        if (!isBell && size == BulletSize.Large) { baseSize *= 1.4f; anchor *= 1.4f; }
        if (ResourceUtils.OpenReadTextureSizeAnchorByConfigFile(key, out var configuredSize, out var configuredAnchor))
        {
            baseSize = configuredSize;
            anchor = isBell ? Vector2.Zero : configuredAnchor;
        }
        var scale = ResourceUtils.TextureSizeScale;
        var worldY = ((IFumenEditorDrawingContext)editor).ConvertToY_DefaultSoflanGroup(new TGrid(1));
        var x = XGridCalculator.ConvertXGridToX(new XGrid(2), editor);
        check(Math.Abs(bounds.Width - baseSize.X * scale) < 0.01 && Math.Abs(bounds.Height - baseSize.Y * scale) < 0.01 &&
              Math.Abs(bounds.X + bounds.Width / 2 - x - anchor.X * scale) < 0.01 &&
              Math.Abs(bounds.Y + bounds.Height / 2 - worldY - anchor.Y * scale) < 0.01,
            name + ": normal sprite uses configured dimensions/anchor at the known snapped coordinate");
        var pixels = PixelBounds(normal);
        var imageTop = Height - (bounds.Bottom - editor.CurrentDrawingTargetContext.ViewRelativeOriginY);
        var imageBottom = Height - (bounds.Top - editor.CurrentDrawingTargetContext.ViewRelativeOriginY);
        check(pixels.Left >= bounds.Left - 1 && pixels.Right <= bounds.Right + 1 &&
              pixels.Top >= imageTop - 1 && pixels.Bottom <= imageBottom + 1,
            name + ": actual pixels lie at the expected scrolled viewport coordinate and anchor");
    }

    private static void AssertSprite(Action<bool, string> check, SKBitmap ghost, SKBitmap normal, string name)
    {
        var expectedBounds = PixelBounds(normal);
        var actualBounds = PixelBounds(ghost);
        check(expectedBounds.Width > 5 && expectedBounds.Height > 5 && Math.Abs(actualBounds.Left - expectedBounds.Left) <= 1 &&
              Math.Abs(actualBounds.Top - expectedBounds.Top) <= 1 && Math.Abs(actualBounds.Right - expectedBounds.Right) <= 1 &&
              Math.Abs(actualBounds.Bottom - expectedBounds.Bottom) <= 1, name + ": raster sprite bounds match normal placement");
        var expected = normal.Pixels;
        var actual = ghost.Pixels;
        var normalPeak = expected.Max(x => (int)x.Alpha);
        var ghostPeak = actual.Max(x => (int)x.Alpha);
        check(ghostPeak > 0 && ghostPeak < normalPeak, name + ": ghost is translucent, not opaque or absent");
        var opacity = ghostPeak / (double)normalPeak;
        var mismatches = 0;
        for (var i = 0; i < expected.Length; i++)
        {
            var a = actual[i];
            var e = expected[i];
            if (Math.Abs(a.Alpha - e.Alpha * opacity) > 2 ||
                (a.Alpha > 32 && e.Alpha > 32 &&
                 (Math.Abs(a.Red - e.Red) > 8 || Math.Abs(a.Green - e.Green) > 8 || Math.Abs(a.Blue - e.Blue) > 8)))
                mismatches++;
        }
        check(mismatches == 0, name + ": every raster pixel matches expected sprite type and anchor (opacity normalized)");
    }

    private static SKRectI PixelBounds(SKBitmap bitmap)
    {
        var left = bitmap.Width;
        var top = bitmap.Height;
        var right = 0;
        var bottom = 0;
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).Alpha > 8)
                {
                    left = Math.Min(left, x); top = Math.Min(top, y);
                    right = Math.Max(right, x + 1); bottom = Math.Max(bottom, y + 1);
                }
        return right == 0 ? SKRectI.Empty : new SKRectI(left, top, right, bottom);
    }

    private static void AssertBlank(Action<bool, string> check, SKBitmap bitmap, string name) =>
        check(bitmap.Pixels.All(x => x.Alpha == 0), name);

    private static void AssertSamePixels(Action<bool, string> check, SKBitmap a, SKBitmap b, string name) =>
        check(a.Width == b.Width && a.Height == b.Height && a.Pixels.SequenceEqual(b.Pixels), name);

    private static void Save(SKBitmap bitmap, string path)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        encoded.SaveTo(stream);
    }

    private static void SaveContactSheet(List<(string Name, SKBitmap Ghost, SKBitmap Normal)> rows, string directory,
        FumenVisualEditorViewModel editor)
    {
        const int crop = 280;
        const int rowHeight = crop + 32;
        using var sheet = new SKBitmap(crop * 2, rows.Count * rowHeight);
        using var canvas = new SKCanvas(sheet);
        canvas.Clear(new SKColor(28, 30, 36));
        using var text = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, 13);
        using var marker = new SKPaint { Color = new SKColor(90, 155, 180), StrokeWidth = 1 };
        var x = (float)XGridCalculator.ConvertXGridToX(new XGrid(2), editor);
        var y = Height - 220f;
        for (var row = 0; row < rows.Count; row++)
        {
            var item = rows[row];
            for (var column = 0; column < 2; column++)
            {
                var left = column * crop;
                var top = row * rowHeight;
                canvas.DrawText(item.Name + (column == 0 ? " / ghost" : " / normal"), left + 5, top + 20, font, text);
                canvas.Save();
                canvas.ClipRect(new SKRect(left, top + 32, left + crop, top + rowHeight));
                var anchorX = left + crop / 2f;
                var anchorY = top + 32 + crop * 0.75f;
                canvas.DrawLine(anchorX - 10, anchorY, anchorX + 10, anchorY, marker);
                canvas.DrawLine(anchorX, anchorY - 10, anchorX, anchorY + 10, marker);
                canvas.DrawBitmap(column == 0 ? item.Ghost : item.Normal, anchorX - x, anchorY - y);
                canvas.Restore();
            }
        }
        Save(sheet, Path.Combine(directory, "rendering-contact-sheet.png"));
    }
}
