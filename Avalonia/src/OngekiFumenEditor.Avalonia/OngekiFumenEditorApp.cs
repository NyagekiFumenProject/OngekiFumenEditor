using Gekimini.Avalonia;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Microsoft.Extensions.DependencyInjection;

namespace OngekiFumenEditor.Avalonia.Avalonia;

public abstract class OngekiFumenEditorApp : App
{
    protected override void RegisterServices(IServiceCollection serviceCollection)
    {
        base.RegisterServices(serviceCollection);

        serviceCollection.AddOngekiFumenEditorAvalonia();

        serviceCollection.AddTypeCollectedActivator(ViewTypeCollectedActivator.Default);

        serviceCollection.AddTypeCollectedActivator(ToolViewModelTypeCollectedActivator.Default);
    }
}

