using Caliburn.Micro;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.ProgramUpdater.Dialogs.ViewModels
{
    public class ShowNewVersionDialogViewModel : Screen
    {
        private IProgramUpdater programUpdater;

        public VersionInfo NewVersionInfo => programUpdater.RemoteVersionInfo;

        public string CurrentVersion => Version.Parse(ThisAssembly.AssemblyFileVersion).ToString(4);

        private bool isReady;
        public bool IsReady
        {
            get => isReady;
            set => Set(ref isReady, value);
        }

        public ShowNewVersionDialogViewModel()
        {
            programUpdater = IoC.Get<IProgramUpdater>();
        }

        public async void StartUpdate()
        {
            try
            {
                // 关掉本对话框，改由进度对话框负责下载/解压/确认执行。
                await TryCloseAsync();
                Log.LogInfo("Opening the update progress dialog.");
                await IoC.Get<IWindowManager>().ShowDialogAsync(new UpdateProgressDialogViewModel());
                Log.LogInfo("Update progress dialog closed.");
            }
            catch (Exception e)
            {
                Log.LogError($"Failed to open the update progress dialog: {e.Message}", e);
            }
        }
    }
}
