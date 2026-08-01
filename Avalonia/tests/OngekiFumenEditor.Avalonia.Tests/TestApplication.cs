using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace OngekiFumenEditor.Avalonia.Tests;

public sealed class TestApplication : global::OngekiFumenEditor.Avalonia.Avalonia.App
{
    private static readonly FieldInfo ServiceProviderField = typeof(global::Gekimini.Avalonia.App)
        .GetField("serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(global::Gekimini.Avalonia.App).FullName, "serviceProvider");

    public TestApplication() : base(isGUIMode: false)
    {
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        RegisterServices(services);
        services.AddOngekiFumenEditorAvalonia();

        // Gekimini owns this field privately; populate it for headless tests while intentionally skipping shell startup.
        ServiceProviderField.SetValue(this, services.BuildServiceProvider());
    }

    protected override void DoExit(int exitCode = 0)
    {
    }
}
