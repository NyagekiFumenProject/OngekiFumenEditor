using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.ProgramUpdater.Dialogs.ViewModels
{
    public class ShowNewVersionDialogViewModel : PropertyChangedBase
    {
        private IProgramUpdater programUpdater;

        public VersionInfo NewVersionInfo => programUpdater.RemoteVersionInfo;

        public string CurrentVersion => Version.Parse(ThisAssembly.AssemblyFileVersion).ToString(3);

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
            await programUpdater.StartUpdate();
        }
    }
}
