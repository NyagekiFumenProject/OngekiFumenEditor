namespace Microsoft.Extensions.DependencyInjection;

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
