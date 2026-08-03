using Microsoft.Extensions.DependencyInjection;

namespace OngekiFumenEditor.Avalonia;

public static class IoC
{
    private static IServiceProvider ServiceProvider =>
        (global::Avalonia.Application.Current as global::Gekimini.Avalonia.App)?.ServiceProvider
        ?? throw new InvalidOperationException("ServiceProvider is not initialized.");

    public static T Get<T>() where T : notnull
    {
        return ServiceProvider.GetService<T>()
               ?? throw new InvalidOperationException($"Service {typeof(T).FullName} is not registered.");
    }

    public static IEnumerable<T> GetAll<T>() where T : notnull
    {
        return ServiceProvider.GetServices<T>();
    }
}


