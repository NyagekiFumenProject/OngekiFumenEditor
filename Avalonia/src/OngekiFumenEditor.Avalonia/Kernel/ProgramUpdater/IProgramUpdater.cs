namespace OngekiFumenEditor.Avalonia.Kernel.ProgramUpdater;

public interface IProgramUpdater
{
    bool HasNewVersion { get; }
    VersionInfo RemoteVersionInfo { get; }

    Task CheckUpdatable();

    Task StartUpdate();

    (int exitCode, string message) CommandExecuteUpdate(UpdaterOption option);
}

