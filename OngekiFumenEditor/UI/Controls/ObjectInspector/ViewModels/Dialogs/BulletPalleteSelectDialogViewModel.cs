using Caliburn.Micro;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels.Dialogs
{
    public class BulletPalleteSelectDialogViewModel : Screen
    {
        private BulletPallete selectedPallete;

        public BulletPalleteSelectDialogViewModel(IEnumerable<BulletPallete> list, BulletPallete initSelectedPallete)
        {
            BulletPalleteList = list;
            SelectedPallete = initSelectedPallete;
        }

        public IEnumerable<BulletPallete> BulletPalleteList { get; }

        public BulletPallete SelectedPallete
        {
            get => selectedPallete;
            set => Set(ref selectedPallete, value);
        }

        public async void OnItemDoubleClick(BulletPallete bulletPallete)
        {
            SelectedPallete = bulletPallete;
            await TryCloseAsync(true);
        }

        public async void OnComfirmButtonClicked()
        {
            await TryCloseAsync(true);
        }

        public async void OnCancelButtonClicked()
        {
            await TryCloseAsync(false);
        }
    }
}
