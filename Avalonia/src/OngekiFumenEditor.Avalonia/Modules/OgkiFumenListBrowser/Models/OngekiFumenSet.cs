#nullable enable

using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Models;

public sealed partial class OngekiFumenSet : ObservableObject
{
    public OngekiFumenSet(
        ISimpleFile musicXmlFile,
        string musicXmlLocator,
        int musicId,
        int musicSourceId,
        string title,
        string artist,
        string genre)
    {
        MusicXmlFile = musicXmlFile ?? throw new ArgumentNullException(nameof(musicXmlFile));
        MusicXmlLocator = musicXmlLocator ?? string.Empty;
        MusicId = musicId;
        MusicSourceId = musicSourceId;
        Title = title ?? string.Empty;
        Artist = artist ?? string.Empty;
        Genre = genre ?? string.Empty;
    }

    public int MusicId { get; }

    public int MusicSourceId { get; }

    public string Title { get; }

    public string Artist { get; }

    public string Genre { get; }

    public ISimpleFile MusicXmlFile { get; }

    public string MusicXmlLocator { get; }

    public List<OngekiFumenDiff> Difficults { get; } = [];

    public ISimpleFile? AudioFile { get; internal set; }

    public string? AudioLocator { get; internal set; }

    public ISimpleFile? AudioAwbFile { get; internal set; }

    public string? AudioAwbLocator { get; internal set; }

    public ISimpleFile? JacketFile { get; internal set; }

    public string? JacketLocator { get; internal set; }

    [ObservableProperty]
    public partial Bitmap? JacketBitmap { get; set; }

    public override string ToString() => $"[{MusicId}] {Artist} - {Title}";
}
