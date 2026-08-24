#pragma warning disable IDE0130 // DI extensions intentionally use the Microsoft namespace for discoverability.
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

public static class DesktopCommandLineServiceCollectionExtensions
{
    public static IServiceCollection AddOngekiFumenEditorDesktopCommandLine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddLogging();
        services.AddOngekiFumenEditorAvalonia();
        services.AddOngekiFumenEditorAvaloniaDesktop();
        return services;
    }
}
