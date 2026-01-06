using Gekimini.Avalonia;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Microsoft.Extensions.DependencyInjection;

namespace OngekiFumenEditor.Avalonia;

public abstract class ExampleApp : App
{
    protected override void RegisterServices(IServiceCollection serviceCollection)
    {
        base.RegisterServices(serviceCollection);

        serviceCollection.AddOngekiFumenEditorAvalonia();

        serviceCollection.AddTypeCollectedActivator(ViewTypeCollectedActivator.Default);

        serviceCollection.AddTypeCollectedActivator(ToolViewModelTypeCollectedActivator.Default);
    }
}