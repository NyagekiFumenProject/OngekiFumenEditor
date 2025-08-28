using Caliburn.Micro;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels.Dialogs;
using OngekiFumenEditor.Utils;
using System;
using System.Windows;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels
{
    public class BulletPalleteTypeUIViewModel : CommonUIViewModelBase<BulletPallete>
    {
        private object cacheStrId = DependencyProperty.UnsetValue;
        public object StrId
        {
            get
            {
                var val = ProxyValue;
                if (val is BulletPallete pallete)
                    return pallete.StrID;
                return cacheStrId;
            }
            set
            {
                var v = value?.ToString()?.Trim() ?? string.Empty;
                cacheStrId = v;
                TryApplyValue(v);
                NotifyOfPropertyChange(() => StrId);
            }
        }

        private void TryApplyValue(string v)
        {
            if (IoC.Get<IFumenObjectPropertyBrowser>().Editor is not { } editor)
            {
                //notify user?
                return;
            }

            var pallete = editor.Fumen.BulletPalleteList.FirstOrDefault(x => x.StrID.Equals(v, StringComparison.CurrentCultureIgnoreCase), default);
            if (pallete is null)
            {
                //notify user?
                return;
            }

            TypedProxyValue = pallete;
            NotifyOfPropertyChange(() => StrId);
        }

        public BulletPalleteTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
        {

        }

        public async void OpenSelectList()
        {
            if (IoC.Get<IFumenObjectPropertyBrowser>().Editor is not { } editor)
            {
                //notify user?
                return;
            }

            var dialog = new BulletPalleteSelectDialogViewModel(editor.Fumen.BulletPalleteList, TypedProxyValue);
            if ((await IoC.Get<IWindowManager>().ShowDialogAsync(dialog)) ?? false)
            {
                var selectedPallete = dialog.SelectedPallete;
                TypedProxyValue = selectedPallete;
            }

            NotifyOfPropertyChange(() => StrId);
        }

        public void SetNull()
        {
            var rollback = TypedProxyValue;
            try
            {
                TypedProxyValue = null;
            }
            catch (Exception e)
            {
                Log.LogError($"Can't set null for prop {PropertyInfo.DisplayPropertyName}: {e.Message}");
                TypedProxyValue = rollback;
            }
        }
    }
}
