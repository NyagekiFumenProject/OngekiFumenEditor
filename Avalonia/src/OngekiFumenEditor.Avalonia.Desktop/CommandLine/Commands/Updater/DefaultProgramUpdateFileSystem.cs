using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Updater;

[RegisterSingleton<IProgramUpdateFileSystem>]
internal sealed class DefaultProgramUpdateFileSystem : IProgramUpdateFileSystem
{
    public IEnumerable<string> GetFiles(string path, string searchPattern, SearchOption searchOption) =>
        Directory.GetFiles(path, searchPattern, searchOption);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public bool FileExists(string path) => File.Exists(path);
    public void MoveFile(string sourceFilePath, string destinationFilePath) =>
        File.Move(sourceFilePath, destinationFilePath);
    public void CopyFile(string sourceFilePath, string destinationFilePath) =>
        File.Copy(sourceFilePath, destinationFilePath);
    public void DeleteFile(string path) => File.Delete(path);
}
