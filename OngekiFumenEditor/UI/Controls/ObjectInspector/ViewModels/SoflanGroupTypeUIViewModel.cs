using Caliburn.Micro;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels
{
    public sealed class SoflanGroupTypeUIViewModel : CommonUIViewModelBase
    {
        public SoflanGroupTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
        {
        }

        public IEnumerable<SoflanGroupWrapItem> Groups =>
            IoC.Get<IFumenObjectPropertyBrowser>().Editor?.Fumen?.IndividualSoflanAreaMap.GetSoflanGroups().ToArray()
            ?? System.Array.Empty<SoflanGroupWrapItem>();

        public object SelectedGroup
        {
            get => PropertyInfo.ProxyValue;
            set
            {
                if (value is not int groupId || PropertyInfo.IsReadOnly)
                    return;
                if (Groups.Any(x => x.SoflanGroupId == groupId) && !Equals(PropertyInfo.ProxyValue, groupId))
                    PropertyInfo.ProxyValue = groupId;
            }
        }

        public void RefreshGroups()
        {
            NotifyOfPropertyChange(nameof(Groups));
            NotifyOfPropertyChange(nameof(SelectedGroup));
        }

        protected override void PropertyInfo_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.PropertyInfo_PropertyChanged(sender, e);
            NotifyOfPropertyChange(nameof(SelectedGroup));
        }
    }
}
