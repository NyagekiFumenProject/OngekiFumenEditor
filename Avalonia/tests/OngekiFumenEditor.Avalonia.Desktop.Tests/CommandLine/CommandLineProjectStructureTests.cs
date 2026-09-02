using System.Xml.Linq;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine;

public sealed class CommandLineProjectStructureTests
{
    [Fact]
    public void CommandLineProject_IsThinDesktopLauncherWithMatchingTargetFrameworkConditions()
    {
        var repositoryRoot = FindRepositoryRoot();
        var commandLineProjectDirectory = Path.Combine(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.CommandLine");
        var commandLineProject = LoadProject(Path.Combine(
            commandLineProjectDirectory,
            "OngekiFumenEditor.Avalonia.CommandLine.csproj"));
        var desktopProject = LoadProject(Path.Combine(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Desktop",
            "OngekiFumenEditor.Avalonia.Desktop.csproj"));

        var projectReference = Assert.Single(commandLineProject.Descendants("ProjectReference"));
        Assert.EndsWith(
            Path.Combine("OngekiFumenEditor.Avalonia.Desktop", "OngekiFumenEditor.Avalonia.Desktop.csproj"),
            NormalizePath(projectReference.Attribute("Include")?.Value),
            StringComparison.OrdinalIgnoreCase);
        Assert.Empty(commandLineProject.Descendants("PackageReference"));
        Assert.Equal(GetTargetFrameworkDefinitions(desktopProject), GetTargetFrameworkDefinitions(commandLineProject));

        var sourceFile = Assert.Single(Directory.GetFiles(commandLineProjectDirectory, "*.cs"));
        var source = File.ReadAllText(sourceFile);
        Assert.Contains("DesktopCommandLineHost.Run(args)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.CommandLine", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ServiceCollection", source, StringComparison.Ordinal);

        var hostSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Desktop",
            "DesktopCommandLineHost.cs"));
        Assert.Contains("isGUIMode: false", hostSource, StringComparison.Ordinal);
        Assert.Contains("ShutdownMode.OnExplicitShutdown", hostSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TestProjects_KeepCoreAndWindowsDesktopReferencesSeparated()
    {
        var repositoryRoot = FindRepositoryRoot();
        var coreTestProject = LoadProject(Path.Combine(
            repositoryRoot,
            "tests",
            "OngekiFumenEditor.Avalonia.Tests",
            "OngekiFumenEditor.Avalonia.Tests.csproj"));
        var desktopTestProject = LoadProject(Path.Combine(
            repositoryRoot,
            "tests",
            "OngekiFumenEditor.Avalonia.Desktop.Tests",
            "OngekiFumenEditor.Avalonia.Desktop.Tests.csproj"));

        var coreReference = Assert.Single(
            coreTestProject.Descendants("ProjectReference"),
            reference => NormalizePath(reference.Attribute("Include")?.Value).EndsWith(
                Path.Combine("OngekiFumenEditor.Avalonia", "OngekiFumenEditor.Avalonia.csproj"),
                StringComparison.OrdinalIgnoreCase));
        Assert.EndsWith(
            Path.Combine("OngekiFumenEditor.Avalonia", "OngekiFumenEditor.Avalonia.csproj"),
            NormalizePath(coreReference.Attribute("Include")?.Value),
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "net11.0-windows10.0.19041.0",
            desktopTestProject.Descendants("TargetFramework").Single().Value.Trim());
        Assert.Contains(
            desktopTestProject.Descendants("ProjectReference"),
            reference => NormalizePath(reference.Attribute("Include")?.Value).EndsWith(
                Path.Combine("OngekiFumenEditor.Avalonia.Desktop", "OngekiFumenEditor.Avalonia.Desktop.csproj"),
                StringComparison.OrdinalIgnoreCase));
    }

    private static XDocument LoadProject(string projectPath) => XDocument.Load(projectPath);

    private static string NormalizePath(string? path) =>
        (path ?? string.Empty).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

    private static (string Condition, string TargetFramework)[] GetTargetFrameworkDefinitions(XDocument project) =>
        project.Descendants("TargetFramework")
            .Select(element => (
                (element.Attribute("Condition")?.Value ?? string.Empty).Trim(),
                element.Value.Trim()))
            .ToArray();

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OngekiFumenEditor.Avalonia.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not find the Avalonia repository root.");
    }
}
