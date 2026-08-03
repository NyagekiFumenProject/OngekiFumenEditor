using System.Xml.Linq;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine.Acb;

public sealed class AcbProjectReferenceTests
{
    [Fact]
    public void DesktopProject_ReferencesAcbGeneratorProjectWithoutDllBindings()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectDirectory = Path.Combine(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Desktop");
        var project = XDocument.Load(Path.Combine(
            projectDirectory,
            "OngekiFumenEditor.Avalonia.Desktop.csproj"));

        var projectReference = Assert.Single(project.Descendants("ProjectReference"), reference =>
            NormalizePath(reference.Attribute("Include")?.Value).EndsWith(
                "Dependencies/AcbGeneratorFuck/src/AcbGeneratorFuck/AcbGeneratorFuck.csproj",
                StringComparison.OrdinalIgnoreCase));

        Assert.Null(projectReference.Attribute("Condition"));
        AssertExistingProject(projectDirectory, projectReference.Attribute("Include")!.Value);
        Assert.DoesNotContain(project.Descendants("Reference"), IsAcbGeneratorBinding);
        Assert.DoesNotContain(project.Descendants("Content"), IsAcbGeneratorBinding);
    }

    [Fact]
    public void AcbGeneratorSubmodule_UsesOfficialRepository()
    {
        var repositoryRoot = FindRepositoryRoot();
        var gitModules = File.ReadAllText(Path.GetFullPath(Path.Combine(
            repositoryRoot,
            "..",
            ".gitmodules")));

        Assert.Contains(
            "path = Avalonia/Dependencies/AcbGeneratorFuck",
            gitModules,
            StringComparison.Ordinal);
        Assert.Contains(
            "url = https://github.com/NyagekiFumenProject/AcbGeneratorFuck",
            gitModules,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AcbGenerateService_UsesManagedProjectWithoutNativeInterop()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceDirectory = Path.Combine(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Desktop",
            "CommandLine",
            "Commands",
            "Acb");
        var serviceSource = File.ReadAllText(Path.Combine(
            sourceDirectory,
            "DefaultAcbGenerateService.cs"));
        var legacyAotArtifact = Path.GetFullPath(Path.Combine(
            repositoryRoot,
            "..",
            "OngekiFumenEditor",
            "Dependencies",
            "AcbGeneratorFuck",
            "AcbGeneratorFuck.aot.dll"));

        Assert.Contains("AcbGeneratorFuck.Generator.Generate(", serviceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NativeAcbGeneratorInterop", serviceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("#if NATIVE_AOT", serviceSource, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(sourceDirectory, "NativeAcbGeneratorInterop.cs")));
        Assert.False(File.Exists(legacyAotArtifact));
    }

    private static bool IsAcbGeneratorBinding(XElement item)
    {
        var path = item.Attribute("Include")?.Value
            ?? item.Element("HintPath")?.Value
            ?? string.Empty;
        return Path.GetFileName(path).StartsWith(
            "AcbGeneratorFuck",
            StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertExistingProject(string projectDirectory, string relativePath)
    {
        var projectPath = Path.GetFullPath(Path.Combine(
            projectDirectory,
            relativePath.Replace(
                Path.AltDirectorySeparatorChar,
                Path.DirectorySeparatorChar)));

        Assert.True(File.Exists(projectPath), $"Referenced ACB generator project was not found: {projectPath}");
    }

    private static string NormalizePath(string? path) =>
        (path ?? string.Empty).Replace('\\', '/');

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
