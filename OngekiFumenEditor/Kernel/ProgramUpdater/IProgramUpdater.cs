using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.ProgramUpdater
{
    public interface IProgramUpdater
    {
        bool HasNewVersion { get; }
        VersionInfo RemoteVersionInfo { get; }

        Task CheckUpdatable();

        Task StartUpdate();

        (int exitCode, string message) CommandExecuteUpdate(UpdaterOption option);
    }
}
