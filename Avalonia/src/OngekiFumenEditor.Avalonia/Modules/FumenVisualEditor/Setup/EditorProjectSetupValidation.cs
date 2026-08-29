#nullable enable

using System.Buffers;
using System.Globalization;
using System.Text;
using DereTore.Exchange.Archive.ACB;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Setup;

public enum PortableEntryNameError
{
    None,
    Empty,
    InvalidUnicode,
    TooLongUtf16,
    TooLongUtf8,
    LeadingOrTrailingWhitespace,
    DotSegment,
    RootedOrMultiSegment,
    TrailingPeriod,
    InvalidCharacter,
    ReservedDeviceName
}

public readonly record struct PortableEntryNameValidationResult(
    PortableEntryNameError Error,
    char? InvalidCharacter = null)
{
    public bool IsValid => Error == PortableEntryNameError.None;
}

public static class PortableEntryNameValidator
{
    private const int MaxNameLength = 255;
    private const int MaxUtf8Length = 255;
    private static readonly SearchValues<char> InvalidCharacters =
        SearchValues.Create("<>:\"/\\|?*");

    public static PortableEntryNameValidationResult Validate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new(PortableEntryNameError.Empty);
        if (name is "." or "..")
            return new(PortableEntryNameError.DotSegment);
        if (Path.IsPathFullyQualified(name) ||
            name.Contains('/') || name.Contains('\\'))
            return new(PortableEntryNameError.RootedOrMultiSegment);
        if (name.Length > MaxNameLength)
            return new(PortableEntryNameError.TooLongUtf16);
        if (Encoding.UTF8.GetByteCount(name) > MaxUtf8Length)
            return new(PortableEntryNameError.TooLongUtf8);
        if (char.IsWhiteSpace(name[0]) || char.IsWhiteSpace(name[^1]))
            return new(PortableEntryNameError.LeadingOrTrailingWhitespace);
        if (name[^1] == '.')
            return new(PortableEntryNameError.TrailingPeriod);

        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
            if (char.IsControl(character) || InvalidCharacters.Contains(character))
                return new(PortableEntryNameError.InvalidCharacter, character);
            if (char.IsHighSurrogate(character))
            {
                if (index + 1 >= name.Length || !char.IsLowSurrogate(name[index + 1]))
                    return new(PortableEntryNameError.InvalidUnicode);
                index++;
            }
            else if (char.IsLowSurrogate(character))
            {
                return new(PortableEntryNameError.InvalidUnicode);
            }
        }

        var stem = name.Split('.', 2)[0];
        if (IsReservedDeviceName(stem))
            return new(PortableEntryNameError.ReservedDeviceName);

        return new(PortableEntryNameError.None);
    }

    public static void ThrowIfInvalid(string? name, string parameterName)
    {
        var result = Validate(name);
        if (!result.IsValid)
            throw new ArgumentException($"The entry name is invalid ({result.Error}).", parameterName);
    }

    private static bool IsReservedDeviceName(string stem)
    {
        if (stem.Length is 0 or > 4)
            return false;
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("NUL", StringComparison.OrdinalIgnoreCase))
            return true;

        return (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
            stem.Length == 4 && stem[3] is >= '1' and <= '9';
    }
}

public enum AcbExternalAwbReferenceError
{
    None,
    MissingLocalPath,
    MissingReference,
    NestedPathUnsupported,
    InvalidReference,
    InvalidAwbName,
    InspectionFailed
}

public sealed record AcbPackageInspection(
    SetupAudioPackageKind Kind,
    string? RequiredExternalAwbLeafName,
    AcbExternalAwbReferenceError Error,
    string? ErrorMessage = null)
{
    public bool IsValid => Error == AcbExternalAwbReferenceError.None;
}

public static class AcbPackageInspector
{
    public static async Task<AcbPackageInspection> InspectAsync(
        ISimpleFile audioFile,
        ISimpleFile? extendAwbAudioFile = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audioFile);
        if (!Path.GetExtension(audioFile.FileName).Equals(".acb", StringComparison.OrdinalIgnoreCase))
            return new(SetupAudioPackageKind.OrdinaryAudio, null, AcbExternalAwbReferenceError.None);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = await audioFile.OpenRead();
            // The DereTore parser only consumes the name for diagnostics; the actual content
            // always comes from the stream, so a virtual file name works on every platform.
            using var acb = AcbFile.FromStream(stream, audioFile.FileName, disposeStream: false);
            if (acb.InternalAwb is not null)
                return new(SetupAudioPackageKind.AcbWithInternalAwb, null, AcbExternalAwbReferenceError.None);

            var reference = acb.ExternalAwb?.FileName;
            if (string.IsNullOrWhiteSpace(reference))
                return new(SetupAudioPackageKind.OrdinaryAudio, null,
                    AcbExternalAwbReferenceError.MissingReference,
                    "The ACB does not declare an external AWB file.");

            if (!TryResolveSiblingLeaf(reference, out var leafName))
                return new(SetupAudioPackageKind.AcbWithExternalAwb, null,
                    AcbExternalAwbReferenceError.NestedPathUnsupported,
                    "Only an external AWB next to the ACB is supported.");

            if (!leafName.EndsWith(".awb", StringComparison.OrdinalIgnoreCase))
                return new(SetupAudioPackageKind.AcbWithExternalAwb, null,
                    AcbExternalAwbReferenceError.InvalidReference,
                    "The ACB external dependency is not an AWB file.");

            var nameValidation = PortableEntryNameValidator.Validate(leafName);
            if (!nameValidation.IsValid)
                return new(SetupAudioPackageKind.AcbWithExternalAwb, null,
                    AcbExternalAwbReferenceError.InvalidAwbName,
                    $"The declared AWB name is not portable: {nameValidation.Error}.");

            return new(SetupAudioPackageKind.AcbWithExternalAwb, leafName,
                AcbExternalAwbReferenceError.None);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new(SetupAudioPackageKind.OrdinaryAudio, null,
                AcbExternalAwbReferenceError.InspectionFailed,
                $"The ACB package cannot be inspected: {exception.Message}");
        }
    }

    private static bool TryResolveSiblingLeaf(
        string reference,
        out string leafName)
    {
        leafName = string.Empty;
        if (string.IsNullOrWhiteSpace(reference))
            return false;
        if (reference.Contains('/') || reference.Contains('\\') ||
            Path.IsPathFullyQualified(reference) ||
            (Uri.TryCreate(reference, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Scheme)))
        {
            return false;
        }

        leafName = reference;
        return true;
    }
}

public static class EditorProjectSetupValidation
{
    public static IReadOnlyList<FumenFormatOption> GetFumenFormatOptions(IFumenParserManager parserManager)
    {
        ArgumentNullException.ThrowIfNull(parserManager);
        var candidates = parserManager.GetSerializerDescriptions()
            .SelectMany(x => x.fileFormat.Select(extension => new FumenFormatOption(x.desc, NormalizeExtension(extension))))
            .ToArray();
        var ambiguous = candidates
            .GroupBy(x => x.Extension, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (ambiguous is not null)
        {
            throw new InvalidDataException(
                $"Multiple fumen serializers declare the extension '{ambiguous.Key}'.");
        }

        var serializers = candidates
            .OrderBy(x => x.Extension, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Extension, StringComparer.Ordinal)
            .Where(option => parserManager.GetSerializer("probe" + option.Extension) is not null &&
                             parserManager.GetDeserializer("probe" + option.Extension) is not null)
            .ToArray();

        return serializers;
    }

    public static string NormalizeExtension(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        return extension.StartsWith('.') ? extension : "." + extension;
    }

    public static bool TryParseBpm(string? text, out double bpm)
    {
        bpm = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var styles = NumberStyles.Float | NumberStyles.AllowThousands;
        if (!double.TryParse(text, styles, CultureInfo.CurrentCulture, out bpm) &&
            !double.TryParse(text, styles, CultureInfo.InvariantCulture, out bpm))
            return false;
        return double.IsFinite(bpm) && bpm > 0;
    }

    public static OngekiFumen CreateBlankFumen(double bpm)
    {
        if (!double.IsFinite(bpm) || bpm <= 0)
            throw new ArgumentOutOfRangeException(nameof(bpm));

        var fumen = new OngekiFumen();
        fumen.MetaInfo.BpmDefinition.First = bpm;
        fumen.MetaInfo.BpmDefinition.Common = bpm;
        fumen.MetaInfo.BpmDefinition.Minimum = bpm;
        fumen.MetaInfo.BpmDefinition.Maximum = bpm;
        fumen.BpmList.FirstBpm = bpm;
        return fumen;
    }

    public static bool IsFileOwnedByDirectory(ISimpleFile file, ISimpleDirectory directory)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(directory);
        for (var parent = file.ParentDictionary; parent is not null; parent = parent.ParentDictionary)
        {
            if (ReferenceEquals(parent, directory))
                return true;
        }

        return false;
    }

    public static bool HasRootConflict(ISimpleDirectory directory, string fileName)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        return directory.ChildFiles.Any(file => file.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)) ||
            directory.ChildDictionaries.Any(child => child.DirectoryName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
    }
}
