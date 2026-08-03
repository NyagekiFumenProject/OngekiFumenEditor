using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Acb;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Convert;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Jacket;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Svg;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Updater;
using OngekiFumenEditor.Avalonia.Modules.FumenConverter;
using OngekiFumenEditor.Avalonia.Modules.PreviewSvgGenerator;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Parser.DefaultImpl;
using System.CommandLine;
using System.CommandLine.Invocation;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine;

public sealed class CommandLineRegistrationTests
{
    [Fact]
    public async Task DesktopRegistration_RootHelpDiscoversFiveCommandsIncludingAcb()
    {
        await using var serviceProvider = CreateServiceProvider();
        var executor = Assert.IsType<DefaultCommandExecutor>(
            serviceProvider.GetRequiredService<ICommandExecutor>());

        var commandNames = executor.RootCommand.Subcommands
            .Select(command => command.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var result = await InvokeAsync(executor.RootCommand, "--help");

        Assert.Equal(new[] { "acb", "convert", "jacket", "svg", "updater" }, commandNames);
        Assert.Equal(0, result.ExitCode);
        Assert.All(commandNames, commandName =>
            Assert.Contains(commandName, result.Output, StringComparison.Ordinal));
        Assert.Equal(string.Empty, result.Error);
    }

    public static TheoryData<string, string[]> CommandHelpCases => new()
    {
        { "acb", ["--musicId", "--inputFile", "--outputFolder", "--previewBegin", "--previewEnd"] },
        { "convert", ["--inputFile", "--outputFile", "--standardize"] },
        { "svg", ["--inputFile", "--outputFile", "--audioFile", "--png"] },
        { "jacket", ["--musicId", "--outputFolder", "--outputWidthSmall", "--outputHeightSmall"] },
        { "updater", ["--sourceFolder", "--targetFolder", "--sourceVersion"] }
    };

    [Theory]
    [MemberData(nameof(CommandHelpCases))]
    public async Task DesktopRegistration_CommandHelpListsMappedOptions(
        string commandName,
        string[] expectedOptions)
    {
        await using var serviceProvider = CreateServiceProvider();
        var executor = Assert.IsType<DefaultCommandExecutor>(
            serviceProvider.GetRequiredService<ICommandExecutor>());

        var result = await InvokeAsync(executor.RootCommand, commandName, "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.All(expectedOptions, option =>
            Assert.Contains(option, result.Output, StringComparison.Ordinal));
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public async Task DesktopRegistration_DefinitionsAndClosedHandlersAreSingletons()
    {
        await using var serviceProvider = CreateServiceProvider();

        var definitionTypes = serviceProvider.GetServices<ICommandLineDefinition>()
            .Select(definition => definition.GetType())
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                typeof(AcbCommandLineDefinition),
                typeof(ConvertCommandLineDefinition),
                typeof(JacketCommandLineDefinition),
                typeof(SvgCommandLineDefinition),
                typeof(UpdaterCommandLineDefinition)
            }.OrderBy(type => type.FullName, StringComparer.Ordinal),
            definitionTypes);
        AssertSingletonHandler<AcbGenerateOption, AcbCommandLineHandler>(serviceProvider);
        AssertSingletonHandler<FumenConvertOption, ConvertCommandLineHandler>(serviceProvider);
        AssertSingletonHandler<SvgGenerateOption, SvgCommandLineHandler>(serviceProvider);
        AssertSingletonHandler<JacketGenerateOption, JacketCommandLineHandler>(serviceProvider);
        AssertSingletonHandler<UpdaterOption, UpdaterCommandLineHandler>(serviceProvider);
    }

    [Fact]
    public async Task DesktopRegistration_UsesOnlyDefaultFumenParserManager()
    {
        await using var serviceProvider = CreateServiceProvider();

        var parserManager = Assert.Single(serviceProvider.GetServices<IFumenParserManager>());

        Assert.IsType<DefaultFumenParserManager>(parserManager);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddOngekiFumenEditorDesktopCommandLine();
        return services.BuildServiceProvider();
    }

    private static void AssertSingletonHandler<TOptions, THandler>(IServiceProvider serviceProvider)
        where THandler : ICommandLineHandler<TOptions>
    {
        var first = serviceProvider.GetRequiredService<ICommandLineHandler<TOptions>>();
        var second = serviceProvider.GetRequiredService<ICommandLineHandler<TOptions>>();

        Assert.IsType<THandler>(first);
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
