using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;

var options = Options.Parse(args);
if (!options.IsValid)
{
    Console.Error.WriteLine("""
Usage:
  RecommendedScriptVerifier --script-root <dir> --reference-dir <dir>

Optional:
  --reference-file <dll>
  --treat-warnings-as-errors
  --obsolete-diagnostic-ids CS0612;CS0618;CS0619
""");
    return 2;
}

var scriptFiles = Directory.Exists(options.ScriptRoot)
    ? Directory.EnumerateFiles(options.ScriptRoot, "*.nyagekiScript", SearchOption.AllDirectories)
        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
        .ToArray()
    : [];

Console.WriteLine($"Recommended script verifier: script root = {options.ScriptRoot}");
Console.WriteLine($"Recommended script verifier: reference dir = {options.ReferenceDir}");
Console.WriteLine($"Recommended script verifier: found {scriptFiles.Length} script(s).");

if (scriptFiles.Length == 0)
{
    Console.WriteLine($"Recommended script verifier: no scripts found under '{options.ScriptRoot}'.");
    return 0;
}

var references = ReferenceResolver.Resolve(options.ReferenceDir, options.ReferenceFiles);
if (references.Count == 0)
{
    Console.Error.WriteLine($"Recommended script verifier: no managed references found under '{options.ReferenceDir}'.");
    return 1;
}

var hasFailure = false;
var parseOptions = CSharpParseOptions.Default
    .WithKind(SourceCodeKind.Script)
    .WithLanguageVersion(LanguageVersion.Preview);
var compilationOptions = new CSharpCompilationOptions(
        OutputKind.DynamicallyLinkedLibrary,
        usings:
        [
            "System",
            "System.IO",
            "System.Diagnostics"
        ])
    .WithOptimizationLevel(OptimizationLevel.Debug)
    .WithPlatform(Platform.AnyCpu);

foreach (var scriptFile in scriptFiles)
{
    var relativeScriptPath = Path.GetRelativePath(options.ScriptRoot, scriptFile);
    Console.WriteLine($"Recommended script verifier: compiling {relativeScriptPath}");

    var script = await File.ReadAllTextAsync(scriptFile, Encoding.UTF8);
    var scriptBytes = Encoding.UTF8.GetBytes(script);
    var sourceText = SourceText.From(scriptBytes, scriptBytes.Length, Encoding.UTF8, canBeEmbedded: true);
    var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, parseOptions, Path.GetFileName(scriptFile));
    var compilation = CSharpCompilation.CreateScriptCompilation(
        Path.GetRandomFileName(),
        syntaxTree,
        references.Values,
        compilationOptions);

    using var peStream = new MemoryStream();
    using var pdbStream = new MemoryStream();
    var emitResult = compilation.Emit(
        peStream,
        pdbStream,
        options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));

    var relevantDiagnostics = emitResult.Diagnostics
        .Where(x => IsFailureDiagnostic(x, options))
        .ToArray();

    if (emitResult.Success && relevantDiagnostics.Length == 0)
    {
        Console.WriteLine($"Recommended script verifier: OK {relativeScriptPath}");
        continue;
    }

    hasFailure = true;
    var diagnosticsToReport = relevantDiagnostics.Length == 0
        ? emitResult.Diagnostics.Where(x => x.Severity >= DiagnosticSeverity.Error).ToArray()
        : relevantDiagnostics;

    Console.Error.WriteLine($"Recommended script verifier: FAILED {relativeScriptPath}");
    Console.Error.WriteLine($"Recommended script verifier: {diagnosticsToReport.Length} failure diagnostic(s) for {relativeScriptPath}.");

    foreach (var diagnostic in diagnosticsToReport)
    {
        Console.Error.WriteLine(diagnostic.ToString());
        Console.Error.WriteLine($"  Id: {diagnostic.Id}");
        Console.Error.WriteLine($"  Severity: {diagnostic.Severity}");
        Console.Error.WriteLine($"  Location: {FormatLocation(diagnostic)}");
        Console.Error.WriteLine($"  Message: {diagnostic.GetMessage()}");
    }
}

return hasFailure ? 1 : 0;

static bool IsFailureDiagnostic(Diagnostic diagnostic, Options options)
{
    if (diagnostic.Severity >= DiagnosticSeverity.Error)
        return true;

    if (options.TreatWarningsAsErrors && diagnostic.Severity == DiagnosticSeverity.Warning)
        return true;

    return diagnostic.Severity == DiagnosticSeverity.Warning
           && (options.ObsoleteDiagnosticIds.Contains(diagnostic.Id)
               || diagnostic.Id.StartsWith("SYSLIB", StringComparison.OrdinalIgnoreCase));
}

static string FormatLocation(Diagnostic diagnostic)
{
    if (diagnostic.Location == Location.None || !diagnostic.Location.IsInSource)
        return "<none>";

    var lineSpan = diagnostic.Location.GetLineSpan();
    var line = lineSpan.StartLinePosition.Line + 1;
    var column = lineSpan.StartLinePosition.Character + 1;
    return $"{lineSpan.Path}({line},{column})";
}

internal sealed record Options(
    string ScriptRoot,
    string ReferenceDir,
    IReadOnlyList<string> ReferenceFiles,
    bool TreatWarningsAsErrors,
    HashSet<string> ObsoleteDiagnosticIds)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(ScriptRoot)
                           && !string.IsNullOrWhiteSpace(ReferenceDir);

    public static Options Parse(string[] args)
    {
        var scriptRoot = string.Empty;
        var referenceDir = string.Empty;
        var referenceFiles = new List<string>();
        var treatWarningsAsErrors = false;
        var obsoleteDiagnosticIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CS0612",
            "CS0618",
            "CS0619"
        };

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--script-root" when i + 1 < args.Length:
                    scriptRoot = args[++i];
                    break;
                case "--reference-dir" when i + 1 < args.Length:
                    referenceDir = args[++i];
                    break;
                case "--reference-file" when i + 1 < args.Length:
                    referenceFiles.Add(args[++i]);
                    break;
                case "--treat-warnings-as-errors":
                    treatWarningsAsErrors = true;
                    break;
                case "--obsolete-diagnostic-ids" when i + 1 < args.Length:
                    obsoleteDiagnosticIds = args[++i]
                        .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    break;
            }
        }

        return new(
            Path.GetFullPath(scriptRoot),
            Path.GetFullPath(referenceDir),
            referenceFiles.Select(Path.GetFullPath).ToArray(),
            treatWarningsAsErrors,
            obsoleteDiagnosticIds);
    }
}

internal static class ReferenceResolver
{
    public static Dictionary<string, MetadataReference> Resolve(string referenceDir, IReadOnlyList<string> referenceFiles)
    {
        var references = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in (((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")) ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            AddReference(references, path);
        }

        if (Directory.Exists(referenceDir))
        {
            foreach (var path in Directory.EnumerateFiles(referenceDir, "*.dll", SearchOption.TopDirectoryOnly))
                AddReference(references, path);
        }

        foreach (var path in referenceFiles)
            AddReference(references, path);

        return references;
    }

    private static void AddReference(IDictionary<string, MetadataReference> references, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || references.ContainsKey(path) || !HasManagedMetadata(path))
            return;

        try
        {
            references[path] = MetadataReference.CreateFromFile(path);
        }
        catch
        {
            // Native or otherwise unreadable DLLs in the app output are not script references.
        }
    }

    private static bool HasManagedMetadata(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var peReader = new PEReader(stream);
            return peReader.HasMetadata;
        }
        catch
        {
            return false;
        }
    }
}
