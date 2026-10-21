using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Caliburn.Micro;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Modules.FumenBulletPalleteListViewer;
using OngekiFumenEditor.Modules.FumenBulletPalleteListViewer.ViewModels;
using OngekiFumenEditor.Modules.FumenBulletPalleteListViewer.Views;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Parser.Ogkr;

internal static class BehaviorChecks
{
    public static void Run(Action<bool, string> check, string repositoryRoot)
    {
        // Materialize the real shared manager before activating test editors.
        var manager = (FumenBulletPalleteListViewerViewModel)IoC.Get<IFumenBulletPalleteListViewer>();
        try
        {
            RequestsAndManager(check, manager, repositoryRoot);
            PreviewAndGestures(check);
            Coordinates(check);
            InvalidationsAndLegacyDrop(check);
            RoutedInputChecks.Run(check);
        }
        finally
        {
            manager.Filter = null;
            manager.OnRefreshFilter();
            manager.SelectedItems.Clear();
            if (IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor is { } active)
                IoC.Get<IEditorDocumentManager>().NotifyDeactivate(active);
        }
    }

    private static BulletPallete AddPallete(FumenVisualEditorViewModel editor, string name)
    {
        var pallete = new BulletPallete { EditorName = name };
        editor.Fumen.AddObject(pallete);
        return pallete;
    }

    private static void RequestsAndManager(Action<bool, string> check, FumenBulletPalleteListViewerViewModel manager, string repositoryRoot)
    {
        using var editor = CheckSupport.CreateEditor();
        var a = AddPallete(editor, "placement-A");
        var b = AddPallete(editor, "placement-B");
        for (var i = 0; i < 100; i++)
            AddPallete(editor, "recycled-" + i);
        check(manager.ToggleBulletPlacementCommand.CanExecute(a), "active manager accepts a chart template");
        check(!manager.ToggleBulletPlacementCommand.CanExecute(new BulletPallete()), "manager rejects foreign templates");
        var view = new FumenBulletPalleteListViewerView();
        ViewModelBinder.Bind(manager, view, null);
        // A non-visible HWND gives ScrollViewer/virtualization a real presentation source
        // without showing or activating a window on the user's desktop.
        using var presentation = new HwndSource(new HwndSourceParameters("PR45 hidden manager check")
        {
            Width = 1200,
            Height = 320,
            PositionX = -32000,
            PositionY = -32000,
            WindowStyle = unchecked((int)0x80000000)
        });
        presentation.RootVisual = view;
        CheckSupport.Arrange(view);
        var list = CheckSupport.Descendants(view).OfType<ListView>().Single();
        CheckBox Box(BulletPallete pallete, bool bell)
        {
            list.ScrollIntoView(pallete);
            CheckSupport.Arrange(view);
            var row = (ListViewItem)list.ItemContainerGenerator.ContainerFromItem(pallete);
            if (row is null)
            {
                var scroll = CheckSupport.Descendants(list).OfType<ScrollViewer>().First();
                throw new InvalidOperationException($"Manager row not realized for {pallete.EditorName}; height={list.ActualHeight}, viewport={scroll.ViewportHeight}, extent={scroll.ExtentHeight}, offset={scroll.VerticalOffset}");
            }
            return CheckSupport.Descendants(row!).OfType<CheckBox>().Single(x =>
                ReferenceEquals(x.Command, bell ? manager.ToggleBellPlacementCommand : manager.ToggleBulletPlacementCommand));
        }
        void ExpectChecked(BulletPallete pallete, bool bell)
        {
            var bulletBox = Box(pallete, false);
            var bellBox = Box(pallete, true);
            check(bulletBox.IsChecked == !bell && bellBox.IsChecked == bell,
                "real one-way row binding reflects selected type for " + pallete.EditorName);
            check(CheckSupport.Descendants(list).OfType<CheckBox>().Count(x => x.IsChecked == true &&
                (ReferenceEquals(x.Command, manager.ToggleBulletPlacementCommand) || ReferenceEquals(x.Command, manager.ToggleBellPlacementCommand))) == 1,
                "only one realized placement checkbox is checked");
        }
        var bullet = Box(a, false);
        bullet.Command.Execute(bullet.CommandParameter);
        CheckSupport.Pump();
        ExpectChecked(a, false);
        var request = editor.RequestBatchPlaceProjectileInfo;
        check(ReferenceEquals(request, manager.RequestBatchPlaceProjectileInfo) && ReferenceEquals(request.Pallete, a) && !request.IsBell,
            "manager and editor expose same immutable Bullet request");
        manager.Editor = editor;
        IoC.Get<IEditorDocumentManager>().NotifyActivate(editor);
        check(ReferenceEquals(request, editor.RequestBatchPlaceProjectileInfo), "same-editor assignment and activation preserve request identity");
        list.SelectedItems.Add(a);
        list.SelectedItems.Add(b);
        CheckSupport.Pump();
        check(ReferenceEquals(request, editor.RequestBatchPlaceProjectileInfo), "row multiselection is independent from placement selection");
        manager.ToggleBellPlacementCommand.Execute(a);
        CheckSupport.Pump();
        ExpectChecked(a, true);
        manager.ToggleBulletPlacementCommand.Execute(b);
        CheckSupport.Pump();
        ExpectChecked(b, false);
        check(Box(a, false).IsChecked == false && Box(a, true).IsChecked == false, "previous template is fully unchecked");
        request = editor.RequestBatchPlaceProjectileInfo;
        manager.Filter = "no-template-matches-this-filter";
        manager.OnRefreshFilter();
        CheckSupport.Arrange(view);
        check(list.Items.Count == 0 && ReferenceEquals(request, editor.RequestBatchPlaceProjectileInfo), "filter hiding every row does not cancel valid request");
        manager.Filter = null;
        manager.OnRefreshFilter();
        CheckSupport.Arrange(view);
        var scroller = CheckSupport.Descendants(list).OfType<ScrollViewer>().First();
        scroller.ScrollToEnd();
        CheckSupport.Arrange(view);
        var last = editor.Fumen.BulletPalleteList.Last();
        check(Box(last, false).IsChecked == false && Box(last, true).IsChecked == false &&
            ReferenceEquals(request, manager.RequestBatchPlaceProjectileInfo),
            "scrolling to an unrelated template does not copy the active check or change the request");
        scroller.ScrollToHome();
        CheckSupport.Arrange(view);
        ExpectChecked(b, false);
        view.Measure(new Size(1700, 320));
        view.Arrange(new Rect(0, 0, 1700, 320));
        view.UpdateLayout();
        var capture = new RenderTargetBitmap(1700, 320, 96, 96, PixelFormats.Pbgra32);
        capture.Render(view);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(capture));
        var directory = Path.Combine(repositoryRoot, "tests", "mcp-e2e", ".tmp", "pr45");
        Directory.CreateDirectory(directory);
        var imagePath = Path.Combine(directory, "manager-placement.png");
        using (var output = File.Create(imagePath))
            encoder.Save(output);
        Console.WriteLine("MANAGER EVIDENCE: " + imagePath);
        CheckSupport.Arrange(view);
        manager.ToggleBulletPlacementCommand.Execute(b);
        CheckSupport.Pump();
        check(editor.RequestBatchPlaceProjectileInfo is null && !manager.IsRequestPlaceBatchProjectilePreview &&
            Box(b, false).IsChecked == false && Box(b, true).IsChecked == false, "toggle same pair clears request and real checked bindings");
        editor.CancelBatchProjectilePlacement();
        check(editor.RequestBatchPlaceProjectileInfo is null && editor.BatchProjectilePlacementPreview is null, "repeated cancellation remains empty");
        manager.ToggleBellPlacementCommand.Execute(a);
        using (var second = CheckSupport.CreateEditor())
        {
            check(editor.RequestBatchPlaceProjectileInfo is null && manager.RequestBatchPlaceProjectileInfo is null,
                "switching active editor cancels old request and manager selection");
        }
        IoC.Get<IShell>().ActiveLayoutItem = editor;
        check(!editor.IsRequestPlaceBatchProjectilePreview, "returning to old editor does not resurrect request");
        view.DataContext = null;
    }

    private static void PreviewAndGestures(Action<bool, string> check)
    {
        using var editor = CheckSupport.CreateEditor();
        var pallete = AddPallete(editor, "preview");
        var selected = new Bell { IsSelected = true };
        editor.Fumen.AddObject(selected);
        var serializer = new DefaultOngekiFumenFormatter();
        var chartBefore = serializer.SerializeAsync(editor.Fumen).GetAwaiter().GetResult();
        var dirtyBefore = editor.IsDirty;
        var historyBefore = editor.UndoRedoManager.UndoActionCount;
        var hitsBefore = editor.GetHits();
        editor.ToggleBatchProjectilePlacement(pallete, false);
        var request = editor.RequestBatchPlaceProjectileInfo;
        editor.OnMouseMove(new Point(430, 350));
        var candidate = editor.BatchProjectilePlacementPreview;
        check(candidate is Bullet && ReferenceEquals(((Bullet)candidate).ReferenceBulletPallete, pallete), "preview is a real Bullet referencing chosen template");
        for (var i = 0; i < 15; i++)
            editor.OnMouseMove(new Point(410 + i, 350 - i));
        check(ReferenceEquals(candidate, editor.BatchProjectilePlacementPreview) && ReferenceEquals(request, editor.RequestBatchPlaceProjectileInfo),
            "pointer movement reuses candidate and request");
        check(chartBefore.SequenceEqual(serializer.SerializeAsync(editor.Fumen).GetAwaiter().GetResult()), "pointer preview preserves entire serialized fumen");
        check(editor.IsDirty == dirtyBefore && editor.UndoRedoManager.UndoActionCount == historyBefore, "pointer preview preserves dirty flag and undo history");
        check(editor.SelectObjects.SequenceEqual(new[] { selected }) && !candidate!.IsSelected, "preview leaves real selection intact and stays unselected");
        check(editor.GetHits().Count == hitsBefore.Count && !editor.GetHits().ContainsKey(candidate), "preview never enters editor hit cache");
        var point = new Point(430, 350);
        var count = editor.Fumen.Bullets.Count;
        check(editor.TryHandleBatchProjectilePlacementMouseDown(MouseButton.Left, point), "placement consumes left press");
        check(editor.Fumen.Bullets.Count == count, "press alone never places");
        var expected = Position(editor.BatchProjectilePlacementPreview!);
        check(editor.TryHandleBatchProjectilePlacementMouseUp(MouseButton.Left, point), "placement consumes left release");
        var placed = editor.Fumen.Bullets.Single();
        check(editor.Fumen.Bullets.Count == count + 1 && Position(placed) == expected && !ReferenceEquals(placed, candidate),
            "click release creates exactly one independent object at preview coordinates");
        check(ReferenceEquals(request, editor.RequestBatchPlaceProjectileInfo), "click preserves continuous request");
        check(!editor.TryHandleBatchProjectilePlacementMouseUp(MouseButton.Left, point) && editor.Fumen.Bullets.Count == count + 1,
            "duplicate release cannot commit twice");
        editor.TryHandleBatchProjectilePlacementMouseDown(MouseButton.Left, point);
        for (var i = 0; i < 12; i++)
            editor.OnMouseMove(new Point(480 + i, 290 - i));
        check(editor.Fumen.Bullets.Count == count + 1, "drag movement does not paint objects");
        var release = new Point(510, 260);
        editor.TryHandleBatchProjectilePlacementMouseUp(MouseButton.Left, release);
        check(editor.Fumen.Bullets.Count == count + 2 && editor.UndoRedoManager.UndoActionCount == historyBefore + 2,
            "drag release commits one object and one history entry");
        var positions = editor.Fumen.Bullets.ToDictionary(x => x, Position);
        editor.UndoRedoManager.Undo(2);
        check(editor.Fumen.Bullets.Count == count, "two placement actions undo separately");
        editor.Setting.ForceXGridIntegerUnit = true;
        editor.ToggleBatchProjectilePlacement(pallete, true);
        editor.OnMouseMove(new Point(600, 100));
        editor.UndoRedoManager.Redo(2);
        check(positions.All(x => editor.Fumen.Bullets.Contains(x.Key) && Position(x.Key) == x.Value), "placement redo retains original objects and coordinates after settings/type/pointer changes");
        editor.TryHandleBatchProjectilePlacementMouseDown(MouseButton.Left, point);
        editor.CancelBatchProjectilePlacement();
        var total = editor.Fumen.Bells.Count + editor.Fumen.Bullets.Count;
        check(editor.TryHandleBatchProjectilePlacementMouseUp(MouseButton.Left, point) && editor.Fumen.Bells.Count + editor.Fumen.Bullets.Count == total,
            "late release after cancellation is consumed without creation");
        editor.ToggleBatchProjectilePlacement(pallete, true);
        editor.TryHandleBatchProjectilePlacementMouseDown(MouseButton.Left, point);
        editor.TryHandleBatchProjectilePlacementMouseUp(MouseButton.Left, point);
        check(editor.Fumen.Bells.Count == 2 && editor.Fumen.Bells.Any(x => ReferenceEquals(x.ReferenceBulletPallete, pallete)), "Bell session commits Bell rather than Bullet");
        check(editor.TryHandleBatchProjectilePlacementMouseDown(MouseButton.Right, point) && !editor.IsRequestPlaceBatchProjectilePreview,
            "right press cancels placement");
        check(editor.TryHandleBatchProjectilePlacementMouseUp(MouseButton.Right, point) && !editor.TryHandleBatchProjectilePlacementMouseUp(MouseButton.Right, point),
            "only matching cancel-right release is consumed");
    }

    private static void Coordinates(Action<bool, string> check)
    {
        using var editor = CheckSupport.CreateEditor();
        var pallete = AddPallete(editor, "coordinates");
        editor.ToggleBatchProjectilePlacement(pallete, false);
        void Expect(Point point, double x, TGrid t, string label, bool move = true)
        {
            if (move) editor.OnMouseMove(point);
            else editor.RefreshBatchProjectilePlacementPreview();
            var preview = editor.BatchProjectilePlacementPreview;
            check(preview is not null && Math.Abs(preview.XGrid.TotalUnit - x) < 0.00001 && Math.Abs(preview.TGrid.TotalUnit - t.TotalUnit) < 0.00001, label);
        }
        var point = new Point(432, 350);
        Expect(point, 0.64, editor.ConvertYToTGrid_DesignMode(250), "disabled T/X snapping preserves exact converted position");
        editor.Setting.DisableXGridMagneticDock = false;
        editor.Setting.ForceXGridMagneticDock = true;
        Expect(point, 1, editor.ConvertYToTGrid_DesignMode(250), "stationary pointer refreshes forced X snap", false);
        editor.Setting.ForceXGridMagneticDock = false;
        Expect(point, 0.64, editor.ConvertYToTGrid_DesignMode(250), "outside magnetic threshold retains fractional X", false);
        Expect(new Point(449, 350), 1, editor.ConvertYToTGrid_DesignMode(250), "near X line snaps within threshold");
        editor.Setting.DisableXGridMagneticDock = true;
        editor.Setting.ForceXGridIntegerUnit = true;
        Expect(new Point(375, 350), -1, editor.ConvertYToTGrid_DesignMode(250), "integer X rounds negative half away from zero");
        Expect(new Point(425, 350), 1, editor.ConvertYToTGrid_DesignMode(250), "integer X rounds positive half away from zero");
        editor.Setting.ForceXGridIntegerUnit = false;
        var beat = new TGrid(0, 480);
        var beatY = editor.ConvertTGridToY_DesignMode(beat);
        CheckSupport.SetViewport(editor, (float)Math.Max(0, beatY - 300));
        editor.Setting.DisableTGridMagneticDock = false;
        var nearBeat = new Point(432, editor.RectInDesignMode.MaxY - beatY - 2);
        Expect(nearBeat, 0.64, beat, "T magnetic threshold snaps to known quarter-measure grid");
        editor.Setting.DisableTGridMagneticDock = true;
        Expect(nearBeat, 0.64, editor.ConvertYToTGrid_DesignMode(beatY + 2), "stationary pointer disabling T snap restores unsnapped grid", false);
        editor.Setting.DisableXGridMagneticDock = false;
        editor.Setting.DisableTGridMagneticDock = false;
        editor.Setting.ForceMagneticDock = true;
        var farBeat = new Point(432, editor.RectInDesignMode.MaxY - beatY - 20);
        Expect(farBeat, 1, beat, "global force snaps both X and T outside ordinary threshold");
        var fixedPosition = Position(editor.BatchProjectilePlacementPreview!);
        editor.TryHandleBatchProjectilePlacementMouseDown(MouseButton.Left, farBeat);
        editor.TryHandleBatchProjectilePlacementMouseUp(MouseButton.Left, farBeat);
        check(Position(editor.Fumen.Bullets.Single()) == fixedPosition, "forced snap preview and committed coordinates are identical");
        editor.Setting.ForceMagneticDock = false;
        editor.Setting.DisableTGridMagneticDock = true;
        editor.Setting.DisableXGridMagneticDock = true;
        CheckSupport.SetViewport(editor);
        editor.OnMouseMove(point);
        var before = Position(editor.BatchProjectilePlacementPreview!);
        CheckSupport.SetViewport(editor, 100);
        Expect(point, 0.64, editor.ConvertYToTGrid_DesignMode(350), "stationary pointer follows viewport scrolling", false);
        check(Position(editor.BatchProjectilePlacementPreview!) != before, "viewport scroll actually changes candidate time");
        editor.Setting.VerticalDisplayScale = 2;
        CheckSupport.SetViewport(editor, 100);
        Expect(point, 0.64, editor.ConvertYToTGrid_DesignMode(350), "stationary pointer follows vertical zoom", false);
        CheckSupport.SetViewport(editor, 100, 1000);
        Expect(point, -1.088, editor.ConvertYToTGrid_DesignMode(350), "stationary pointer follows viewport width change", false);
        editor.OnMouseMove(new Point(-1, 20));
        check(editor.BatchProjectilePlacementPreview is null && editor.IsRequestPlaceBatchProjectilePreview, "outside viewport hides preview but retains request");
        editor.OnMouseMove(point);
        check(editor.BatchProjectilePlacementPreview is not null, "returning pointer restores preview");
        editor.Setting.DisableXGridMagneticDock = true;
        editor.Setting.ForceXGridMagneticDock = true;
        editor.RefreshBatchProjectilePlacementPreview();
        check(editor.BatchProjectilePlacementPreview is null, "conflicting forced/disabled snapping has no valid stale candidate");
        var count = editor.Fumen.Bullets.Count;
        editor.TryHandleBatchProjectilePlacementMouseDown(MouseButton.Left, point);
        editor.TryHandleBatchProjectilePlacementMouseUp(MouseButton.Left, point);
        check(editor.Fumen.Bullets.Count == count, "invalid coordinate resolution cannot reuse old placement");
        editor.Setting.ForceXGridMagneticDock = false;
        foreach (var invalid in new[] { new Point(double.NaN, 1), new Point(1, double.PositiveInfinity), new Point(1000, 1), new Point(1, 600) })
        {
            editor.OnMouseMove(invalid);
            check(editor.BatchProjectilePlacementPreview is null, "nonfinite/outside pointer rejected: " + invalid);
        }
        // TotalDurationHeight is clamped to ViewHeight; scroll beyond that minimum first.
        CheckSupport.SetViewport(editor, 1000, 1000);
        editor.OnMouseMove(point);
        var durationPosition = Position(editor.BatchProjectilePlacementPreview!);
        var durationRequest = editor.RequestBatchPlaceProjectileInfo;
        var durationHistory = editor.UndoRedoManager.UndoActionCount;
        editor.TryHandleBatchProjectilePlacementMouseDown(MouseButton.Left, point);
        editor.TotalDurationHeight = 10;
        editor.RefreshBatchProjectilePlacementPreview();
        check(editor.BatchProjectilePlacementPreview is null && ReferenceEquals(durationRequest, editor.RequestBatchPlaceProjectileInfo),
            "stationary pointer hides cached candidate when duration shrinks without cancelling request");
        check(editor.TryHandleBatchProjectilePlacementMouseUp(MouseButton.Left, point) && editor.Fumen.Bullets.Count == count &&
            editor.UndoRedoManager.UndoActionCount == durationHistory,
            "release after duration shrinks consumes gesture without stale placement or history");
        editor.TotalDurationHeight = 100000;
        editor.RefreshBatchProjectilePlacementPreview();
        check(editor.BatchProjectilePlacementPreview is { } restored && Position(restored) == durationPosition &&
            ReferenceEquals(durationRequest, editor.RequestBatchPlaceProjectileInfo),
            "stationary pointer restores exact candidate when duration expands again");
    }

    private static void InvalidationsAndLegacyDrop(Action<bool, string> check)
    {
        using var editor = CheckSupport.CreateEditor();
        var pallete = AddPallete(editor, "lifecycle");
        editor.ToggleBatchProjectilePlacement(pallete, false);
        editor.Fumen.BulletPalleteList.RemovePallete(pallete);
        check(!editor.IsRequestPlaceBatchProjectilePreview, "direct template collection removal cancels session");
        editor.Fumen.AddObject(pallete);
        editor.ToggleBatchProjectilePlacement(pallete, false);
        editor.EditorProjectData.Fumen = new OngekiFumen();
        check(!editor.IsRequestPlaceBatchProjectilePreview && editor.BatchProjectilePlacementPreview is null, "project fumen replacement clears old template/candidate");
        pallete = AddPallete(editor, "legacy");
        editor.ToggleBatchProjectilePlacement(pallete, true);
        editor.SwitchMode(true);
        check(!editor.IsDesignMode && !editor.CanRequestBatchProjectilePlacement && !editor.IsRequestPlaceBatchProjectilePreview,
            "preview mode transition cancels request and disallows placement");
        editor.ToggleBatchProjectilePlacement(pallete, false);
        check(!editor.IsRequestPlaceBatchProjectilePreview, "cannot start placement outside design mode");
        editor.SwitchMode(false);
        CheckSupport.SetViewport(editor);
        var bullet = new Bullet { ReferenceBulletPallete = pallete };
        new OngekiObjectDropParam(() => bullet).Drop(editor, new Point(432, 250));
        var original = Position(bullet);
        check(editor.Fumen.Bullets.Contains(bullet) && Math.Abs(bullet.XGrid.TotalUnit - 0.64) < 0.00001 &&
            bullet.TGrid == editor.ConvertYToTGrid_DesignMode(250) && !editor.IsRequestPlaceBatchProjectilePreview,
            "legacy OngekiObjectDropParam remains a single placement with exact coordinate conversion");
        editor.UndoRedoManager.Undo(1);
        bullet.TGrid = new TGrid(99, 0);
        bullet.XGrid = new XGrid(7, 0);
        editor.Setting.ForceXGridIntegerUnit = true;
        editor.ToggleBatchProjectilePlacement(pallete, true);
        editor.UndoRedoManager.Redo(1);
        check(editor.Fumen.Bullets.Contains(bullet) && Position(bullet) == original, "legacy redo restores fixed coordinates despite later object/settings mutation");
        editor.CancelBatchProjectilePlacement();
        new OngekiObjectDropParam(() => new Bell { ReferenceBulletPallete = pallete }).Drop(editor, new Point(450, 300));
        check(editor.Fumen.Bells.Count == 1 && !editor.IsRequestPlaceBatchProjectilePreview, "legacy Bell drop remains single-shot");
        editor.ToggleBatchProjectilePlacement(pallete, false);
        editor.Dispose();
        check(!editor.IsRequestPlaceBatchProjectilePreview && !editor.CanRequestBatchProjectilePlacement, "editor disposal cancels session permanently");
    }

    internal static (double T, double X) Position(OngekiMovableObjectBase obj) => (obj.TGrid.TotalUnit, obj.XGrid.TotalUnit);
}
