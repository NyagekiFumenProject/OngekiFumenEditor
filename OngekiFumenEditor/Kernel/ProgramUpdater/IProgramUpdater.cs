using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.ProgramUpdater
{
    public interface IProgramUpdater
    {
        bool HasNewVersion { get; }
        VersionInfo RemoteVersionInfo { get; }

        Task CheckUpdatable();

        /// <summary>下载并解压更新包到临时目录；用户取消时抛 OperationCanceledException。</summary>
        Task PrepareUpdateAsync(IProgress<UpdatePrepareProgress> progress, CancellationToken cancellationToken);

        /// <summary>启动更新器进程并关闭当前程序；只能在 PrepareUpdateAsync 成功返回后调用。</summary>
        void LaunchPreparedUpdate();

        (int exitCode, string message) CommandExecuteUpdate(UpdaterOption option);
    }
}
