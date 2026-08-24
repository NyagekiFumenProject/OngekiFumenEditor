#nullable enable

namespace OngekiFumenEditor.Avalonia.Utils.Logs;

/// <summary>
/// Marks an <see cref="ILogOutput"/> that persists records into its platform-owned log folder.
/// </summary>
public interface IFileLogOutput : ILogOutput
{
    /// <summary>
    /// Session marker written once at the head of every log file, inherited from the original WPF sink.
    /// </summary>
    public const string BeginFileLogOutputMarker = "----------BEGIN FILE LOG OUTPUT----------\n";

    /// <summary>
    /// Gets a user-facing description of the actual log folder.
    /// </summary>
    string LogDirectoryPath { get; }
}
