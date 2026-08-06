using Gekimini.Avalonia.Modules.Toolbox.Models;
using Gekimini.Avalonia.Modules.Toolbox.Services;
using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.EditorObjects;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Toolboxes.EditorObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Toolboxes.OngekiObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenVisualEditor;

public sealed class ToolboxGeneratorRegistrationTests
{
    private static readonly (Type GeneratorType, string Name, string Category, Type ObjectType)[] ExpectedItems =
    [
        (typeof(AutoPlayFaderLaneStartToolboxGenerator), "AutoPlayFaderLane", "Misc", typeof(AutoplayFaderLaneStart)),
        (typeof(CommentToolboxGenerator), "Comment", "Misc", typeof(Comment)),
        (typeof(InterpolatableSoflanToolboxGenerator), "Interpolatable Soflan", "Soflan", typeof(InterpolatableSoflan)),
        (typeof(KeyframeSoflanToolboxGenerator), "Keyframe Soflan", "Soflan", typeof(KeyframeSoflan)),
        (typeof(BeamStartToolboxGenerator), "Beam Start", "Ongeki Objects", typeof(BeamStart)),
        (typeof(BellToolboxGenerator), "Bell", "Ongeki Objects", typeof(Bell)),
        (typeof(BPMChangeToolboxGenerator), "BPM Change", "Ongeki Objects", typeof(BPMChange)),
        (typeof(BulletToolboxGenerator), "Bullet", "Ongeki Objects", typeof(Bullet)),
        (typeof(ClickSEToolboxGenerator), "Click SE", "Ongeki Objects", typeof(ClickSE)),
        (typeof(EnemySetToolboxGenerator), "Enemy Set", "Ongeki Objects", typeof(EnemySet)),
        (typeof(FlickToolboxGenerator), "Flick", "Ongeki Objects", typeof(Flick)),
        (typeof(HoldToolboxGenerator), "Hold Start", "Ongeki Objects", typeof(Hold)),
        (typeof(IndividualSoflanAreaToolboxGenerator), "Individual Soflan Area", "Ongeki Objects", typeof(IndividualSoflanArea)),
        (typeof(LaneBlockAreaToolboxGenerator), "Lane Block", "Lane Control", typeof(LaneBlockArea)),
        (typeof(LaneLeftStartToolboxGenerator), "Lane Left(Red) Start", "Ongeki Lanes", typeof(LaneLeftStart)),
        (typeof(LaneCenterStartToolboxGenerator), "Lane Center(Green) Start", "Ongeki Lanes", typeof(LaneCenterStart)),
        (typeof(LaneRightStartToolboxGenerator), "Lane Right(Blue) Start", "Ongeki Lanes", typeof(LaneRightStart)),
        (typeof(LaneColorfulStartToolboxGenerator), "Lane Colorful Start", "Ongeki Lanes", typeof(ColorfulLaneStart)),
        (typeof(EnemyLaneStartToolboxGenerator), "Enemy Lane Start", "Ongeki Lanes", typeof(EnemyLaneStart)),
        (typeof(MeterChangeToolboxGenerator), "Meter Change", "Ongeki Objects", typeof(MeterChange)),
        (typeof(SoflanToolboxGenerator), "Duration Soflan", "Soflan", typeof(Soflan)),
        (typeof(TapToolboxGenerator), "Tap", "Ongeki Objects", typeof(Tap)),
        (typeof(WallLeftStartToolboxGenerator), "Wall Left Start", "Ongeki Lanes", typeof(WallLeftStart)),
        (typeof(WallRightStartToolboxGenerator), "Wall Right Start", "Ongeki Lanes", typeof(WallRightStart))
    ];

    [Fact]
    public void AddOngekiFumenEditorAvalonia_RegistersAllFumenToolboxGeneratorsAsSingletonToolboxItems()
    {
        var services = CreateServices();

        var descriptors = services
            .Where(service => service.ServiceType == typeof(ToolboxItem) &&
                              service.ImplementationType is not null &&
                              typeof(IToolboxGenerator).IsAssignableFrom(service.ImplementationType))
            .ToArray();

        Assert.Equal(ExpectedItems.Length, descriptors.Length);
        Assert.All(descriptors, descriptor => Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime));
        Assert.Equal(
            ExpectedItems.Select(item => item.GeneratorType).OrderBy(type => type.FullName, StringComparer.Ordinal),
            descriptors.Select(descriptor => descriptor.ImplementationType!)
                .OrderBy(type => type.FullName, StringComparer.Ordinal));
    }

    [Fact]
    public void ToolboxService_ForFumenVisualEditor_ReturnsAllGeneratorsWithMetadataAndFreshObjects()
    {
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();
        var toolboxService = new ToolboxService(provider, provider.GetServices<ToolboxItem>());

        var items = toolboxService.GetToolboxItems(typeof(FumenVisualEditorViewModel)).ToArray();

        Assert.Equal(ExpectedItems.Length, items.Length);
        foreach (var expected in ExpectedItems)
        {
            var item = Assert.Single(items, candidate => candidate.GetType() == expected.GeneratorType);
            Assert.Equal(typeof(FumenVisualEditorViewModel).FullName, item.DocumentType);
            Assert.Equal(expected.Name, item.Name.Text);
            Assert.Equal(expected.Category, item.Category.Text);
            Assert.Equal(expected.Category, item.CategoryGroupId);

            var generator = Assert.IsAssignableFrom<IToolboxGenerator>(item);
            var first = generator.CreateDisplayObject();
            var second = generator.CreateDisplayObject();
            Assert.Equal(expected.ObjectType, first.GetType());
            Assert.Equal(expected.ObjectType, second.GetType());
            Assert.NotSame(first, second);
        }
    }

    [Fact]
    public void AddOngekiFumenEditorAvalonia_DoesNotRegisterSvgPrefabToolboxItemsWhileFeatureIsDisabled()
    {
        var services = CreateServices();

        var registeredTypes = services
            .Where(service => service.ServiceType == typeof(ToolboxItem))
            .Select(service => service.ImplementationType)
            .ToArray();

        Assert.DoesNotContain(typeof(SvgImageFilePrefabToolboxGenerator), registeredTypes);
        Assert.DoesNotContain(typeof(SvgStringPrefabToolboxGenerator), registeredTypes);
    }

    [Fact]
    public void DefaultToolBoxDropAction_BulletGenerator_PreservesCustomPaletteInitialization()
    {
        var action = new ExposedDefaultToolBoxDropAction(new BulletToolboxGenerator());

        var bullet = Assert.IsType<Bullet>(action.CreateDisplayObject());

        Assert.Same(BulletPallete.DummyCustomPallete, bullet.ReferenceBulletPallete);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddOngekiFumenEditorAvalonia();
        return services;
    }

    private sealed class ExposedDefaultToolBoxDropAction : DefaultToolBoxDropAction
    {
        public ExposedDefaultToolBoxDropAction(ToolboxItem toolboxItem) : base(toolboxItem)
        {
        }

        public OngekiObjectBase CreateDisplayObject() => GetDisplayObject();
    }
}
