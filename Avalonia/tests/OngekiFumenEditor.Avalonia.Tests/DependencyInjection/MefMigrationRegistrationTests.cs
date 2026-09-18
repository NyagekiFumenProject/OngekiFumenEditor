using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.KeyBinding;
using OngekiFumenEditor.Avalonia.Kernel.Scheduler;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.UIGenerator.ObjectOperationImplement;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.UIGenerator.ObjectsOperationImplement;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.SVG;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.Beam;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.Holds;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.Soflans;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.DependencyInjection;

public sealed class MefMigrationRegistrationTests
{
    [Fact]
    public void AddOngekiFumenEditorAvalonia_RegistersMigratedDrawingTargetsAsSingletons()
    {
        var services = CreateServices();

        AssertRegistrations<IFumenEditorDrawingTarget>(
            services,
            ServiceLifetime.Singleton,
            typeof(TapDrawingTarget),
            typeof(DurationSoflanDrawingTarget),
            typeof(LaneBlockerDrawingTarget),
            typeof(CommonHorizonalDrawingTarget),
            typeof(FlickDrawingTarget),
            typeof(IndividualSoflanAreaDrawingTarget),
            typeof(HoldDrawingTarget),
            typeof(HoldTapDrawingTarget),
            typeof(LaneCurvePathControlDrawingTarget),
            typeof(BeamLazerDrawingTarget));
    }

    [Fact]
    public void AddOngekiFumenEditorAvalonia_RegistersMigratedObjectOperationGeneratorsAsSingletons()
    {
        var services = CreateServices();

        AssertRegistrations<IOngekiObjectOperationGenerator>(
            services,
            ServiceLifetime.Singleton,
            typeof(BeamOperationGenerator),
            typeof(HoldOperationGenerator),
            typeof(InterpolatableSoflanOperationGenerator),
            typeof(LaneStartOperationGenerator),
            typeof(WallStartOperationGenerator));

        AssertRegistrations<IOngekiMultiObjectsOperationGenerator>(
            services,
            ServiceLifetime.Singleton,
            typeof(MultiLanesStartOperationGenerator));
    }

    [Fact]
    public void AddOngekiFumenEditorAvalonia_DoesNotRegisterSvgPrefabEditorServicesWhileFeatureIsDisabled()
    {
        var services = CreateServices();

        Assert.DoesNotContain(services, service =>
            service.ServiceType == typeof(IFumenEditorDrawingTarget) &&
            service.ImplementationType == typeof(SvgObjectDrawingTarget));
        Assert.DoesNotContain(services, service =>
            service.ServiceType == typeof(IOngekiObjectOperationGenerator) &&
            service.ImplementationType == typeof(SvgPrefabOperationGenerator));
    }



    [Fact]
    public void AddOngekiFumenEditorAvalonia_DoesNotRegisterPlatformKeyBindingManager()
    {
        var services = CreateServices();

        Assert.DoesNotContain(
            services,
            service => service.ServiceType == typeof(IKeyBindingManager));
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddOngekiFumenEditorAvalonia();
        return services;
    }

    private static void AssertRegistrations<TService>(
        IServiceCollection services,
        ServiceLifetime expectedLifetime,
        params Type[] expectedImplementationTypes)
    {
        var descriptors = services
            .Where(service => service.ServiceType == typeof(TService))
            .ToArray();

        foreach (var implementationType in expectedImplementationTypes)
        {
            var descriptor = Assert.Single(
                descriptors,
                service => service.ImplementationType == implementationType);
            Assert.Equal(expectedLifetime, descriptor.Lifetime);
        }
    }
}
