namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Updater;

internal interface IProgramUpdateFileSystem
{
    IEnumerable<string> GetFiles(string path, string searchPattern, SearchOption searchOption);
    void CreateDirectory(string path);
    bool FileExists(string path);
    void MoveFile(string sourceFilePath, string destinationFilePath);
    void CopyFile(string sourceFilePath, string destinationFilePath);
    void DeleteFile(string path);
}
