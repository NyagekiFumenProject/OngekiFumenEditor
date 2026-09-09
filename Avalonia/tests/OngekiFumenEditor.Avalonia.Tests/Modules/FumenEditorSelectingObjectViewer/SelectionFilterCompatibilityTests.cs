#nullable enable
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.EditorObjects;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.Base.SelectionFilter;
using OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.ViewModels;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenEditorSelectingObjectViewer;

public sealed class SelectionFilterCompatibilityTests
{

    [AvaloniaTheory]
    [InlineData("tag")]
    [InlineData("lane-node")]
    [InlineData("curve-next")]
    [InlineData("curve-prev")]
    [InlineData("critical")]
    [InlineData("flick")]
    [InlineData("dock-lane")]
    [InlineData("hold-type")]
    [InlineData("bullet-palette")]
    [InlineData("bullet-size")]
    [InlineData("bullet-type")]
    [InlineData("lane-block-direction")]
    [InlineData("lane-block-type")]
    [InlineData("soflan-type")]
    public void RestoredOptionFilters_MatchAndRejectOriginalScenarios(string scenarioName)
    {
        using var context = new ViewerContext();
        var scenario = CreateScenario(context, scenarioName);

        Assert.Equal(FilterOptionResult.Match, scenario.Option.Filter(scenario.Match));
        Assert.Equal(FilterOptionResult.NoMatch, scenario.Option.Filter(scenario.NoMatch));
    }

    [AvaloniaFact]
    public void ActiveEditorAndFumenChanges_RebindPaletteWithoutRetainingOldEntries()
    {
        var firstPalette = CreatePalette("A0", "First");
        var firstFumen = new OngekiFumen();
        firstFumen.BulletPalleteList.AddPallete(firstPalette);

        using var context = new ViewerContext(firstFumen);
        var option = GetOption<BulletPaletteFilterOption>(
            context.Viewer.SelectionFilter,
            Lang.SelectionFilter_OptionLabelBulletPalette);
        Assert.Equal([firstPalette], GetChartPalettes(option));

        var firstLatePalette = CreatePalette("A1", "First late");
        firstFumen.BulletPalleteList.AddPallete(firstLatePalette);
        Assert.Equal([firstPalette, firstLatePalette], GetChartPalettes(option));

        var secondPalette = CreatePalette("B0", "Second");
        var secondFumen = new OngekiFumen();
        secondFumen.BulletPalleteList.AddPallete(secondPalette);
        context.Editor.EditorContext.Fumen = secondFumen;
        Assert.Equal([secondPalette], GetChartPalettes(option));

        firstFumen.BulletPalleteList.AddPallete(CreatePalette("A2", "Detached"));
        Assert.Equal([secondPalette], GetChartPalettes(option));

        var thirdPalette = CreatePalette("C0", "Third");
        var thirdEditor = context.Activate(new OngekiFumen { });
        thirdEditor.EditorContext.Fumen.BulletPalleteList.AddPallete(thirdPalette);
        Assert.Equal([thirdPalette], GetChartPalettes(option));

        context.Manager.Activate(null);
        Assert.Empty(GetChartPalettes(option));
    }

    [AvaloniaFact]
    public void ApplyFilterToSelection_RefreshesEditorViewerAndPropertyBrowser()
    {
        var retained = new Tap { IsCritical = true, IsSelected = true };
        var removed = new Tap { IsCritical = false, IsSelected = true };
        var fumen = new OngekiFumen();
        fumen.AddObject(retained);
        fumen.AddObject(removed);

        using var context = new ViewerContext(fumen);
        var browser = IoC.Get<IFumenObjectPropertyBrowser>();

        try
        {
            context.Viewer.IsFilterMenuVisible = true;
            browser.RefreshSelected(context.Editor);
            var criticalOption = GetOption<BooleanOption>(
                context.Viewer.SelectionFilter,
                Lang.SelectionFilter_OptionLabelIsCritical);
            criticalOption.Value = true;
            criticalOption.IsEnabled = true;

            context.Viewer.SelectionFilter.ApplyFilterToSelection();

            Assert.True(retained.IsSelected);
            Assert.False(removed.IsSelected);
            Assert.Same(retained, Assert.Single(context.Editor.SelectObjects));
            Assert.Same(retained, Assert.Single(browser.SelectedObjects));
            Assert.Same(retained, Assert.Single(context.Viewer.EditorSelectObjects.Cast<SelectedObjectRow>()).Object);
        }
        finally
        {
            browser.RefreshSelected((FumenVisualEditorViewModel)null!);
        }
    }

    [AvaloniaFact]
    public void SelectOnlyItemsOfSelectedTypeCommand_RestrictsEditorSelection()
    {
        var tap = new Tap { IsSelected = true };
        var bell = new Bell { IsSelected = true };
        var fumen = new OngekiFumen();
        fumen.AddObject(tap);
        fumen.AddObject(bell);

        using var context = new ViewerContext(fumen);
        context.Viewer.RefreshCommand.Execute(null);
        context.Viewer.SelectedItems.Add(Assert.Single(
            context.Viewer.EditorSelectObjects.Cast<SelectedObjectRow>(),
            row => ReferenceEquals(row.Object, tap)));

        context.Viewer.SelectOnlyItemsOfSelectedTypeCommand.Execute(null);

        Assert.True(tap.IsSelected);
        Assert.False(bell.IsSelected);
    }

    [AvaloniaFact]
    public void DeselectItemsOfSelectedTypeCommand_RemovesOnlySelectedTypes()
    {
        var tap = new Tap { IsSelected = true };
        var bell = new Bell { IsSelected = true };
        var fumen = new OngekiFumen();
        fumen.AddObject(tap);
        fumen.AddObject(bell);

        using var context = new ViewerContext(fumen);
        context.Viewer.RefreshCommand.Execute(null);
        context.Viewer.SelectedItems.Add(Assert.Single(
            context.Viewer.EditorSelectObjects.Cast<SelectedObjectRow>(),
            row => ReferenceEquals(row.Object, tap)));

        context.Viewer.DeselectItemsOfSelectedTypeCommand.Execute(null);

        Assert.False(tap.IsSelected);
        Assert.True(bell.IsSelected);
    }

    [AvaloniaFact]
    public void SelectionCommands_TrackSelectedRows()
    {
        var tap = new Tap { IsSelected = true };
        var fumen = new OngekiFumen();
        fumen.AddObject(tap);

        using var context = new ViewerContext(fumen);
        Assert.False(context.Viewer.CancelSelectedObjectsCommand.CanExecute(null));
        Assert.False(context.Viewer.SelectOnlyItemsOfSelectedTypeCommand.CanExecute(null));
        Assert.False(context.Viewer.DeselectItemsOfSelectedTypeCommand.CanExecute(null));

        var row = Assert.Single(context.Viewer.EditorSelectObjects.Cast<SelectedObjectRow>());
        context.Viewer.SelectedItems.Add(row);

        Assert.True(context.Viewer.CancelSelectedObjectsCommand.CanExecute(null));
        Assert.True(context.Viewer.SelectOnlyItemsOfSelectedTypeCommand.CanExecute(null));
        Assert.True(context.Viewer.DeselectItemsOfSelectedTypeCommand.CanExecute(null));

        context.Viewer.SelectedItems.Clear();
        Assert.False(context.Viewer.CancelSelectedObjectsCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void MissingHoldEndpoints_AreUnselectedRelatives()
    {
        using var context = new ViewerContext();
        var option = GetOption<HeadTailSpecificationOption<Hold, HoldEnd>>(
            context.Viewer.SelectionFilter, Lang.SelectionFilter_OptionLabelHoldType);
        var head = new Hold();
        var tail = new HoldEnd();

        option.TypedValue = HeadTailSpecification.HeadNoChild;
        option.IncrementOptionMatchCount(head);
        option.IncrementOptionMatchCount(tail);
        Assert.Equal(FilterOptionResult.Match, option.Filter(head));
        Assert.Equal(1, option.SelectedOptionMatchCount);
        option.TypedValue = HeadTailSpecification.HeadWithChild;
        Assert.Equal(FilterOptionResult.NoMatch, option.Filter(head));
        option.TypedValue = HeadTailSpecification.TailNoParent;
        option.IncrementOptionMatchCount(tail);
        Assert.Equal(FilterOptionResult.Match, option.Filter(tail));
        Assert.Equal(1, option.SelectedOptionMatchCount);
        option.TypedValue = HeadTailSpecification.TailWithParent;
        Assert.Equal(FilterOptionResult.NoMatch, option.Filter(tail));
    }

    [AvaloniaFact]
    public void ReenabledType_StillAppliesEnabledOptions()
    {
        var tap = new Tap { IsSelected = true, IsCritical = false };
        var bell = new Bell { IsSelected = true };
        var fumen = new OngekiFumen();
        fumen.AddObjects([tap, bell]);
        using var context = new ViewerContext(fumen);
        var filter = context.Viewer.SelectionFilter;
        var tapType = Assert.Single(filter.FilterTypeCategories.SelectMany(category => category.Items),
            item => item.Types.Contains(typeof(Tap)));
        tapType.IsSelected = false;
        GetOption<BooleanOption>(filter, Lang.SelectionFilter_OptionLabelIsCritical).IsEnabled = true;
        tapType.IsSelected = true;

        try
        {
            filter.ApplyFilterToSelection();
            Assert.False(tap.IsSelected);
            Assert.True(bell.IsSelected);
        }
        finally
        {
            IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected((FumenVisualEditorViewModel)null!);
        }
    }

    [AvaloniaFact]
    public void PaletteCollectionChange_RecomputesUnassignedFallback()
    {
        var palette = CreatePalette("A0", "Assigned");
        var assigned = new Bullet { ReferenceBulletPallete = palette, IsSelected = true };
        var unassigned = new Bell { IsSelected = true };
        var fumen = new OngekiFumen();
        fumen.BulletPalleteList.AddPallete(palette);
        fumen.AddObjects([assigned, unassigned]);
        using var context = new ViewerContext(fumen);
        var option = GetOption<BulletPaletteFilterOption>(context.Viewer.SelectionFilter,
            Lang.SelectionFilter_OptionLabelBulletPalette);
        option.IsEnabled = true;
        Assert.Single(option.Items, item => ReferenceEquals(item.Palette, palette)).IsSelected = true;

        fumen.BulletPalleteList.AddPallete(CreatePalette("A1", "New"));
        Assert.Equal(1, Assert.Single(option.Items, item => item.Palette is null).BellCount);
        Assert.Equal(1, Assert.Single(option.Items, item => ReferenceEquals(item.Palette, palette)).BulletCount);
        try
        {
            context.Viewer.SelectionFilter.ApplyFilterToSelection();
            Assert.False(assigned.IsSelected);
            Assert.True(unassigned.IsSelected);
        }
        finally
        {
            IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected((FumenVisualEditorViewModel)null!);
        }
    }

    [AvaloniaFact]
    public void SelectionMutationAndEditorSwitch_RefreshHiddenFilter()
    {
        var tap = new Tap { IsSelected = true };
        var fumen = new OngekiFumen();
        fumen.AddObject(tap);
        using var context = new ViewerContext(fumen);
        tap.IsSelected = false;
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(context.Viewer.EditorSelectObjects);
        tap.IsSelected = true;
        Dispatcher.UIThread.RunJobs();
        Assert.Same(tap, Assert.Single(context.Viewer.EditorSelectObjects.Cast<SelectedObjectRow>()).Object);

        var bpm = new BPMChange { IsSelected = true, TGrid = new TGrid(1) };
        var nextFumen = new OngekiFumen();
        nextFumen.AddObject(bpm);
        context.Activate(nextFumen);
        tap.IsSelected = false;
        try
        {
            context.Viewer.SelectionFilter.ApplyFilterToSelection();
            Assert.True(bpm.IsSelected);
            Assert.Same(bpm, Assert.Single(context.Viewer.EditorSelectObjects.Cast<SelectedObjectRow>()).Object);
        }
        finally
        {
            IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected((FumenVisualEditorViewModel)null!);
        }
    }

    [AvaloniaFact]
    public void InvertedFilter_RemovesSnapshotDespiteSelectionRefresh()
    {
        var first = new Tap { IsSelected = true };
        var second = new Tap { IsSelected = true };
        var fumen = new OngekiFumen();
        fumen.AddObjects([first, second]);
        using var context = new ViewerContext(fumen);
        context.Viewer.IsFilterMenuVisible = true;
        context.Viewer.SelectionFilter.IsInvertFilter = true;
        try
        {
            context.Viewer.SelectionFilter.ApplyFilterToSelection();
            Assert.False(first.IsSelected);
            Assert.False(second.IsSelected);
            Assert.Empty(context.Viewer.EditorSelectObjects);
        }
        finally
        {
            IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected((FumenVisualEditorViewModel)null!);
        }
    }

    private static FilterScenario CreateScenario(ViewerContext context, string scenarioName)
    {
        var filter = context.Viewer.SelectionFilter;
        return scenarioName switch
        {
            "tag" => CreateTagScenario(filter),
            "lane-node" => CreateLaneNodeScenario(filter),
            "curve-next" => CreateCurveNextScenario(filter),
            "curve-prev" => CreateCurvePreviousScenario(filter),
            "critical" => CreateCriticalScenario(filter),
            "flick" => CreateFlickScenario(filter),
            "dock-lane" => CreateDockLaneScenario(filter),
            "hold-type" => CreateHoldScenario(filter),
            "bullet-palette" => CreateBulletPaletteScenario(context),
            "bullet-size" => CreateBulletSizeScenario(filter),
            "bullet-type" => CreateBulletTypeScenario(filter),
            "lane-block-direction" => CreateLaneBlockDirectionScenario(filter),
            "lane-block-type" => CreateLaneBlockTypeScenario(filter),
            "soflan-type" => CreateSoflanTypeScenario(filter),
            _ => throw new ArgumentOutOfRangeException(nameof(scenarioName), scenarioName, null)
        };
    }

    private static FilterScenario CreateTagScenario(SelectionFilterViewModel filter)
    {
        var option = GetOption<TextWithRegexOption>(filter, Lang.SelectionFilter_OptionLabelTag);
        option.InputText = "keep";
        return new(option, new Tap { Tag = "keep" }, new Tap { Tag = "drop" });
    }

    private static FilterScenario CreateLaneNodeScenario(SelectionFilterViewModel filter)
    {
        var option = GetOption<LaneNodeSpecificationOption>(filter, Lang.SelectionFilter_OptionLabelLaneNodeType);
        option.TypedValue = HeadTailSpecification.Head;
        return new(option, new LaneLeftStart(), new LaneLeftNext());
    }

    private static FilterScenario CreateCurveNextScenario(SelectionFilterViewModel filter)
    {
        var option = GetOption<EnumSpecificationOption<SelectionStatusSpecification>>(
            filter,
            Lang.SelectionFilter_OptionLabelCurveNextSelected);
        option.TypedValue = SelectionStatusSpecification.Selected;
        return new(option, CreateCurveControl(nextSelected: true, previousSelected: false),
            CreateCurveControl(nextSelected: false, previousSelected: false));
    }

    private static FilterScenario CreateCurvePreviousScenario(SelectionFilterViewModel filter)
    {
        var option = GetOption<EnumSpecificationOption<SelectionStatusSpecification>>(
            filter,
            Lang.SelectionFilter_OptionLabelCurvePrevSelected);
        option.TypedValue = SelectionStatusSpecification.Selected;
        return new(option, CreateCurveControl(nextSelected: false, previousSelected: true),
            CreateCurveControl(nextSelected: false, previousSelected: false));
    }

    private static FilterScenario CreateCriticalScenario(SelectionFilterViewModel filter)
    {
        var option = GetOption<BooleanOption>(filter, Lang.SelectionFilter_OptionLabelIsCritical);
        option.Value = true;
        return new(option, new Tap { IsCritical = true }, new Tap { IsCritical = false });
    }

    private static FilterScenario CreateFlickScenario(SelectionFilterViewModel filter)
    {
        var option = GetOption<BooleanOption>(filter, Lang.SelectionFilter_OptionLabelFlickDirection);
        option.Value = true;
        return new(option,
            new Flick { Direction = Flick.FlickDirection.Left },
            new Flick { Direction = Flick.FlickDirection.Right });
    }

    private static FilterScenario CreateDockLaneScenario(SelectionFilterViewModel filter)
    {
        var option = GetOption<DockableObjectLaneFilterOption>(filter, Lang.SelectionFilter_OptionLabelDockLanes);
        Assert.Single(option.Values, value => value.DockLane == DockableTargetSpecification.LaneLeft).IsSelected = true;
        return new(option,
            new Tap { ReferenceLaneStart = new LaneLeftStart() },
            new Tap { ReferenceLaneStart = new LaneRightStart() });
    }

    private static FilterScenario CreateHoldScenario(SelectionFilterViewModel filter)
    {
        var option = GetOption<HeadTailSpecificationOption<Hold, HoldEnd>>(
            filter,
            Lang.SelectionFilter_OptionLabelHoldType);
        option.TypedValue = HeadTailSpecification.Head;
        var hold = new Hold();
        var end = new HoldEnd();
        hold.SetHoldEnd(end);
        return new(option, hold, end);
    }

    private static FilterScenario CreateBulletPaletteScenario(ViewerContext context)
    {
        var first = CreatePalette("D0", "Selected");
        var second = CreatePalette("D1", "Rejected");
        context.Editor.EditorContext.Fumen.BulletPalleteList.AddPallete(first);
        context.Editor.EditorContext.Fumen.BulletPalleteList.AddPallete(second);
        var option = GetOption<BulletPaletteFilterOption>(
            context.Viewer.SelectionFilter,
            Lang.SelectionFilter_OptionLabelBulletPalette);
        Assert.Single(option.Items, item => ReferenceEquals(item.Palette, first)).IsSelected = true;
        return new(option,
            new Bullet { ReferenceBulletPallete = first },
            new Bullet { ReferenceBulletPallete = second });
    }

    private static FilterScenario CreateBulletSizeScenario(SelectionFilterViewModel filter)
    {
        var option = GetOption<BooleanOption>(filter, Lang.BulletSize);
        option.Value = true;
        return new(option,
            new Bullet { SizeValue = BulletSize.Normal },
            new Bullet { SizeValue = BulletSize.Large });
    }

    private static FilterScenario CreateBulletTypeScenario(SelectionFilterViewModel filter)
    {
        var option = GetOption<EnumSpecificationOption<BulletType>>(filter, Lang.BulletType);
        option.TypedValue = BulletType.Circle;
        return new(option,
            new Bullet { TypeValue = BulletType.Circle },
            new Bullet { TypeValue = BulletType.Needle });
    }

    private static FilterScenario CreateLaneBlockDirectionScenario(SelectionFilterViewModel filter)
    {
        var option = GetOption<BooleanOption>(filter, Lang.SelectionFilter_OptionLabelLaneBlockDirection);
        option.Value = true;
        return new(option,
            new LaneBlockArea { Direction = LaneBlockArea.BlockDirection.Left },
            new LaneBlockArea { Direction = LaneBlockArea.BlockDirection.Right });
    }

    private static FilterScenario CreateLaneBlockTypeScenario(SelectionFilterViewModel filter)
    {
        var option = GetOption<HeadTailSpecificationOption<LaneBlockArea, LaneBlockArea.LaneBlockAreaEndIndicator>>(
            filter,
            Lang.SelectionFilter_OptionLabelLaneBlockType);
        option.TypedValue = HeadTailSpecification.Head;
        var laneBlock = new LaneBlockArea();
        return new(option, laneBlock, laneBlock.EndIndicator);
    }

    private static FilterScenario CreateSoflanTypeScenario(SelectionFilterViewModel filter)
    {
        var option = GetOption<HeadTailSpecificationOption<Soflan, Soflan.SoflanEndIndicator>>(
            filter,
            Lang.SelectionFilter_OptionLabelSoflanAreaType);
        option.TypedValue = HeadTailSpecification.Head;
        var soflan = new Soflan();
        return new(option, soflan, soflan.EndIndicator);
    }

    private static LaneCurvePathControlObject CreateCurveControl(bool nextSelected, bool previousSelected)
    {
        var start = new LaneLeftStart { IsSelected = previousSelected };
        var next = new LaneLeftNext { IsSelected = nextSelected };
        start.AddChildObject(next);
        return new LaneCurvePathControlObject { RefCurveObject = next };
    }

    private static BulletPallete CreatePalette(string id, string name) => new()
    {
        StrID = id,
        EditorName = name
    };

    private static BulletPallete[] GetChartPalettes(BulletPaletteFilterOption option) => option.Items
        .Select(item => item.Palette)
        .Where(palette => palette is not null)
        .Cast<BulletPallete>()
        .ToArray();

    private static TOption GetOption<TOption>(SelectionFilterViewModel filter, string text)
        where TOption : SelectionFilterOption
    {
        return Assert.IsType<TOption>(Assert.Single(
            filter.OptionCategories.SelectMany(category => category.Options),
            option => option.Text == text));
    }


    private sealed record FilterScenario(
        SelectionFilterOption Option,
        OngekiObjectBase Match,
        OngekiObjectBase NoMatch);


    private sealed class ViewerContext : IDisposable
    {
        private readonly List<FumenVisualEditorViewModel> editors = [];

        public TestEditorDocumentManager Manager { get; }
        public FumenVisualEditorViewModel Editor { get; }
        public FumenEditorSelectingObjectViewerViewModel Viewer { get; }

        public ViewerContext(OngekiFumen? fumen = null)
        {
            Editor = new FumenVisualEditorViewModel()
            {
                EditorContext = new EditorContext { Fumen = fumen ?? new OngekiFumen() }
            };
            editors.Add(Editor);
            Manager = new TestEditorDocumentManager(Editor);
            Viewer = new FumenEditorSelectingObjectViewerViewModel(Manager);
        }

        public FumenVisualEditorViewModel Activate(OngekiFumen fumen)
        {
            var editor = new FumenVisualEditorViewModel()
            {
                EditorContext = new EditorContext { Fumen = fumen }
            };
            editors.Add(editor);
            Manager.Activate(editor);
            return editor;
        }

        public void Dispose()
        {
            Manager.Activate(null);
            foreach (var editor in editors.Distinct())
                editor.Setting.Dispose();
        }
    }

    private sealed class TestEditorDocumentManager(FumenVisualEditorViewModel initialEditor) : IEditorDocumentManager
    {
        private FumenVisualEditorViewModel? current = initialEditor;
        private event IEditorDocumentManager.ActivateEditorChangedFunc? activateEditorChanged;

        public FumenVisualEditorViewModel CurrentActivatedEditor => current!;

        public event IEditorDocumentManager.NotifyCreateFunc OnNotifyCreated
        {
            add { }
            remove { }
        }

        public event IEditorDocumentManager.ActivateEditorChangedFunc OnActivateEditorChanged
        {
            add => activateEditorChanged += value;
            remove => activateEditorChanged -= value;
        }

        public event IEditorDocumentManager.NotifyDestoryFunc OnNotifyDestoryed
        {
            add { }
            remove { }
        }

        public void Activate(FumenVisualEditorViewModel? editor)
        {
            var old = current;
            current = editor;
            activateEditorChanged?.Invoke(editor!, old!);
        }

        public IEnumerable<FumenVisualEditorViewModel> GetCurrentEditors() => current is null ? [] : [current];

        public void NotifyActivate(FumenVisualEditorViewModel editor) => Activate(editor);

        public void NotifyDeactivate(FumenVisualEditorViewModel editor)
        {
            if (ReferenceEquals(current, editor))
                Activate(null);
        }

        public void NotifyCreate(FumenVisualEditorViewModel editor) => Activate(editor);

        public void NotifyDestory(FumenVisualEditorViewModel editor)
        {
            if (ReferenceEquals(current, editor))
                Activate(null);
        }
    }
}
