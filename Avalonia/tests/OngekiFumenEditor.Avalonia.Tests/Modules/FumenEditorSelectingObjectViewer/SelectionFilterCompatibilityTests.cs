#nullable enable
using Avalonia.Headless.XUnit;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.EditorObjects;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.Base.SelectionFilter;
using OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.ViewModels;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenEditorSelectingObjectViewer;

public sealed class SelectionFilterCompatibilityTests
{
    [AvaloniaFact]
    public void Constructor_RestoresOriginalObjectTypesAndFourteenOptions()
    {
        using var context = new ViewerContext();
        var filter = context.Viewer.SelectionFilter;

        Assert.Equal(5, filter.FilterTypeCategories.Count);
        Assert.Equal(25, filter.FilterTypeCategories.Sum(category => category.Items.Count));
        AssertCategory(filter.FilterTypeCategories[0], Lang.SelectionFilterObjectCategoryLane,
            (Lang.WallLeft, [typeof(WallLeftNext), typeof(WallLeftStart)]),
            (Lang.LaneLeft, [typeof(LaneLeftNext), typeof(LaneLeftStart)]),
            (Lang.LaneCenter, [typeof(LaneCenterNext), typeof(LaneCenterStart)]),
            (Lang.LaneRight, [typeof(LaneRightNext), typeof(LaneRightStart)]),
            (Lang.WallRight, [typeof(WallRightNext), typeof(WallRightStart)]),
            (Lang.LaneColorful, [typeof(ColorfulLaneNext), typeof(ColorfulLaneStart)]),
            (Lang.EnemyLane, [typeof(EnemyLaneNext), typeof(EnemyLaneStart)]),
            (Lang.AutoPlayFaderLane, [typeof(AutoplayFaderLaneNext), typeof(AutoplayFaderLaneStart)]),
            (Lang.Beam, [typeof(BeamNext), typeof(BeamStart)]),
            (Lang.CurveControlPoint, [typeof(LaneCurvePathControlObject)]));
        AssertCategory(filter.FilterTypeCategories[1], Lang.SelectionFilterObjectCategoryDockable,
            (Lang.Tap, [typeof(Tap)]),
            (Lang.Hold, [typeof(Hold), typeof(HoldEnd)]));
        AssertCategory(filter.FilterTypeCategories[2], Lang.SelectionFilterObjectCategoryFloating,
            (Lang.Bell, [typeof(Bell)]),
            (Lang.Bullet, [typeof(Bullet)]),
            (Lang.Flick, [typeof(Flick)]));
        AssertCategory(filter.FilterTypeCategories[3], Lang.SelectionFilterObjectCategoryTimeline,
            (Lang.LaneBlock, [typeof(LaneBlockArea), typeof(LaneBlockArea.LaneBlockAreaEndIndicator)]),
            (Lang.ClickSE, [typeof(ClickSE)]),
            (Lang.InterpolatableSoflan, [typeof(InterpolatableSoflan), typeof(InterpolatableSoflan.InterpolatableSoflanIndicator)]),
            (Lang.KeyframeSoflan, [typeof(KeyframeSoflan)]),
            (Lang.DurationSoflan, [typeof(IDurationSoflan)]),
            (Lang.MeterChange, [typeof(MeterChange)]),
            (Lang.IndividualSoflanArea, [typeof(IndividualSoflanArea)]));
        AssertCategory(filter.FilterTypeCategories[4], Lang.SelectionFilterObjectCategoryMisc,
            (Lang.SvgPrefabFile, [typeof(SvgImageFilePrefab)]),
            (Lang.SvgPrefabText, [typeof(SvgStringPrefab)]),
            (Lang.Comment, [typeof(Comment)]));

        Assert.Equal(5, filter.OptionCategories.Count);
        Assert.Equal(14, filter.OptionCategories.Sum(category => category.Options.Count));
        AssertOptionCategory(filter.OptionCategories[0], Lang.SelectionFilter_OptionTabGeneral,
            (Lang.SelectionFilter_OptionLabelTag, typeof(TextWithRegexOption)));
        AssertOptionCategory(filter.OptionCategories[1], Lang.SelectionFilter_OptionTabLanes,
            (Lang.SelectionFilter_OptionLabelLaneNodeType, typeof(LaneNodeSpecificationOption)),
            (Lang.SelectionFilter_OptionLabelCurveNextSelected, typeof(EnumSpecificationOption<SelectionStatusSpecification>)),
            (Lang.SelectionFilter_OptionLabelCurvePrevSelected, typeof(EnumSpecificationOption<SelectionStatusSpecification>)));
        AssertOptionCategory(filter.OptionCategories[2], Lang.SelectionFilter_OptionTabHitObjects,
            (Lang.SelectionFilter_OptionLabelIsCritical, typeof(BooleanOption)),
            (Lang.SelectionFilter_OptionLabelFlickDirection, typeof(BooleanOption)),
            (Lang.SelectionFilter_OptionLabelDockLanes, typeof(DockableObjectLaneFilterOption)),
            (Lang.SelectionFilter_OptionLabelHoldType, typeof(HeadTailSpecificationOption<Hold, HoldEnd>)));
        AssertOptionCategory(filter.OptionCategories[3], Lang.SelectionFilter_OptionTabBullets,
            (Lang.SelectionFilter_OptionLabelBulletPalette, typeof(BulletPaletteFilterOption)),
            (Lang.BulletSize, typeof(BooleanOption)),
            (Lang.BulletType, typeof(EnumSpecificationOption<BulletType>)));
        AssertOptionCategory(filter.OptionCategories[4], Lang.SelectionFilter_OptionTabOther,
            (Lang.SelectionFilter_OptionLabelLaneBlockDirection, typeof(BooleanOption)),
            (Lang.SelectionFilter_OptionLabelLaneBlockType,
                typeof(HeadTailSpecificationOption<LaneBlockArea, LaneBlockArea.LaneBlockAreaEndIndicator>)),
            (Lang.SelectionFilter_OptionLabelSoflanAreaType,
                typeof(HeadTailSpecificationOption<Soflan, Soflan.SoflanEndIndicator>)));
    }

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
        context.Editor.Fumen = secondFumen;
        Assert.Equal([secondPalette], GetChartPalettes(option));

        firstFumen.BulletPalleteList.AddPallete(CreatePalette("A2", "Detached"));
        Assert.Equal([secondPalette], GetChartPalettes(option));

        var thirdPalette = CreatePalette("C0", "Third");
        var thirdEditor = context.Activate(new OngekiFumen { });
        thirdEditor.Fumen.BulletPalleteList.AddPallete(thirdPalette);
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
            Assert.Contains("1", context.Viewer.SelectionFilter.FilterOutcomeText, StringComparison.Ordinal);
        }
        finally
        {
            browser.RefreshSelected((FumenVisualEditorViewModel)null!);
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
        context.Editor.Fumen.BulletPalleteList.AddPallete(first);
        context.Editor.Fumen.BulletPalleteList.AddPallete(second);
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
        .Where(palette => palette is not null && !ReferenceEquals(palette, BulletPallete.DummyCustomPallete))
        .Cast<BulletPallete>()
        .ToArray();

    private static TOption GetOption<TOption>(SelectionFilterViewModel filter, string text)
        where TOption : SelectionFilterOption
    {
        return Assert.IsType<TOption>(Assert.Single(
            filter.OptionCategories.SelectMany(category => category.Options),
            option => option.Text == text));
    }

    private static void AssertCategory(
        FilterObjectTypeCategory category,
        string categoryName,
        params (string Text, Type[] Types)[] expectedItems)
    {
        Assert.Equal($"{categoryName} (0)", category.CategoryNameDisplay);
        Assert.Equal(expectedItems.Select(item => item.Text), category.Items.Select(item => item.Text));
        Assert.Equal(expectedItems.Select(item => item.Types), category.Items.Select(item => item.Types), TypeArrayComparer.Instance);
    }

    private static void AssertOptionCategory(
        OptionCategory category,
        string categoryName,
        params (string Text, Type Type)[] expectedOptions)
    {
        Assert.Equal(categoryName, category.Name);
        Assert.Equal(expectedOptions.Select(option => option.Text), category.Options.Select(option => option.Text));
        Assert.Equal(expectedOptions.Select(option => option.Type), category.Options.Select(option => option.GetType()));
    }

    private sealed record FilterScenario(
        SelectionFilterOption Option,
        OngekiObjectBase Match,
        OngekiObjectBase NoMatch);

    private sealed class TypeArrayComparer : IEqualityComparer<Type[]>
    {
        public static TypeArrayComparer Instance { get; } = new();

        public bool Equals(Type[]? x, Type[]? y) =>
            ReferenceEquals(x, y) || x is not null && y is not null && x.SequenceEqual(y);

        public int GetHashCode(Type[] obj) => obj.Aggregate(17, (hash, type) => HashCode.Combine(hash, type));
    }

    private sealed class ViewerContext : IDisposable
    {
        private readonly List<FumenVisualEditorViewModel> editors = [];

        public TestEditorDocumentManager Manager { get; }
        public FumenVisualEditorViewModel Editor { get; }
        public FumenEditorSelectingObjectViewerViewModel Viewer { get; }

        public ViewerContext(OngekiFumen? fumen = null)
        {
            Editor = new FumenVisualEditorViewModel { Fumen = fumen ?? new OngekiFumen() };
            editors.Add(Editor);
            Manager = new TestEditorDocumentManager(Editor);
            Viewer = new FumenEditorSelectingObjectViewerViewModel(Manager);
        }

        public FumenVisualEditorViewModel Activate(OngekiFumen fumen)
        {
            var editor = new FumenVisualEditorViewModel { Fumen = fumen };
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
