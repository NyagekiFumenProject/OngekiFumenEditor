using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Acb;
using System.CommandLine;
using System.CommandLine.Invocation;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine.Acb;

public sealed class AcbCommandRegistrationTests
{
    [Fact]
    public async Task DesktopRegistration_RootAndCommandHelpDiscoverAcbWithExpectedOptions()
    {
        await using var serviceProvider = CreateServiceProvider();
        var executor = Assert.IsType<DefaultCommandExecutor>(
            serviceProvider.GetRequiredService<ICommandExecutor>());

        var command = Assert.Single(
            executor.RootCommand.Subcommands,
            static candidate => candidate.Name.Equals("acb", StringComparison.Ordinal));
        var rootHelp = await InvokeAsync(executor.RootCommand, "--help");
        var commandHelp = await InvokeAsync(executor.RootCommand, "acb", "--help");

        Assert.Equal("acb", command.Name);
        Assert.Equal(0, rootHelp.ExitCode);
        Assert.Contains("acb", rootHelp.Output, StringComparison.Ordinal);
        Assert.Equal(string.Empty, rootHelp.Error);
        Assert.Equal(0, commandHelp.ExitCode);
        Assert.All(
            new[] { "--musicId", "--inputFile", "--outputFolder", "--previewBegin", "--previewEnd" },
            option => Assert.Contains(option, commandHelp.Output, StringComparison.Ordinal));
        Assert.Equal(string.Empty, commandHelp.Error);
    }

    [Fact]
    public async Task DesktopRegistration_ResolvesAcbDefinitionHandlerAndServiceAsSingletons()
    {
        await using var serviceProvider = CreateServiceProvider();

        Assert.Contains(
            serviceProvider.GetServices<ICommandLineDefinition>(),
            static definition => definition is AcbCommandLineDefinition);
        AssertSingleton<ICommandLineHandler<AcbGenerateOption>, AcbCommandLineHandler>(serviceProvider);
        AssertSingleton<IAcbGenerateService, DefaultAcbGenerateService>(serviceProvider);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddOngekiFumenEditorDesktopCommandLine();
        return services.BuildServiceProvider();
    }

    private static void AssertSingleton<TService, TImplementation>(IServiceProvider serviceProvider)
        where TService : class
        where TImplementation : class, TService
    {
        var first = serviceProvider.GetRequiredService<TService>();
        var second = serviceProvider.GetRequiredService<TService>();

        Assert.IsType<TImplementation>(first);
        Assert.Same(first, second);
    }

    private static async Task<InvocationResult> InvokeAsync(RootCommand rootCommand, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var configuration = new InvocationConfiguration
        {
            Output = output,
            Error = error
        };

        var exitCode = await rootCommand.Parse(args).InvokeAsync(configuration);
        return new InvocationResult(exitCode, output.ToString(), error.ToString());
    }

    private sealed record InvocationResult(int ExitCode, string Output, string Error);
}
