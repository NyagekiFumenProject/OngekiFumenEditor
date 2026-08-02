using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Parser.DefaultImpl;

namespace Microsoft.Extensions.DependencyInjection;

public static class CommandLineServiceCollectionExtensions
{
    public static IServiceCollection AddOngekiFumenEditorCommandLine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddLogging();
        services.AddOngekiFumenEditorAvalonia();

        // The old Nyageki namespace still contains a legacy parser-manager registration.
        // Register the complete implementation last until that compatibility type is removed.
        services.AddSingleton<IFumenParserManager, DefaultFumenParserManager>();
        services.AddOngekiFumenEditorAvaloniaCommandLine();
        return services;
    }
}
