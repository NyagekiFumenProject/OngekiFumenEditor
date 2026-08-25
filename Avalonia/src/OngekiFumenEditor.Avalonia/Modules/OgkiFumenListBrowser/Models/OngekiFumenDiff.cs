#nullable enable

using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Models;

public sealed class OngekiFumenDiff
{
    public OngekiFumenDiff(OngekiFumenSet referenceSet)
    {
        RefSet = referenceSet ?? throw new ArgumentNullException(nameof(referenceSet));
    }

    public OngekiFumenSet RefSet { get; }

    public float Level { get; init; }

    public int DiffIdx { get; init; }

    public float Bpm { get; set; }

    public string Creator { get; set; } = string.Empty;

    /// <summary>
    /// The chart capability owned by the selected directory tree.
    /// </summary>
    public ISimpleFile FumenFile { get; init; } = null!;

    /// <summary>
    /// A slash-separated locator relative to the selected root directory.
    /// </summary>
    public string FumenLocator { get; init; } = string.Empty;

    public ISimpleFile File => FumenFile;

    public string RelativePath => FumenLocator;

    public string DiffName => DiffIdx switch
    {
        0 => "Basic",
        1 => "Advanced",
        2 => "Expert",
        3 => "Master",
        4 => "Lunatic",
        _ => string.Empty
    };
}
