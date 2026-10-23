using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels.Dialogs;
using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer;
using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels
{
    /// <summary>属性浏览器中的变速组选择按钮：点击后弹出组列表对话框。</summary>
    public sealed class SoflanGroupTypeUIViewModel : CommonUIViewModelBase
    {
        public SoflanGroupTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
        {
        }

        public string DisplayText
        {
            get
            {
                var value = PropertyInfo.ProxyValue;
                if (ReferenceEquals(value, DependencyProperty.UnsetValue))
                    return Resources.SoflanGroupMixedValue;
                if (value is not int id)
                    return string.Empty;

                var editor = IoC.Get<IFumenObjectPropertyBrowser>().Editor;
                var group = editor?.Fumen?.IndividualSoflanAreaMap.SoflanGroupTree.GetGroup(id);
                return group is null ? $"#{id}" : SoflanGroupDisplayName.FormatGroupWithId(group);
            }
        }

        public async void SelectGroup()
        {
            var editor = IoC.Get<IFumenObjectPropertyBrowser>().Editor;
            var tree = editor?.Fumen?.IndividualSoflanAreaMap.SoflanGroupTree;
            if (tree is null)
                return;

            var currentId = PropertyInfo.ProxyValue is int id ? id : (int?)null;
            var viewer = IoC.Get<IFumenSoflanGroupListViewer>();
            var dialog = new SoflanGroupSelectDialogViewModel(
                tree,
                currentId,
                viewer.GetNextSoflanGroupId,
                viewer.CreateGroupWithDefaultName);
            if ((await IoC.Get<IWindowManager>().ShowDialogAsync(dialog)) != true
                || dialog.SelectedGroupId is not int selectedId)
                return;

            PropertyInfo.ProxyValue = selectedId;
            NotifyOfPropertyChange(nameof(DisplayText));
        }

        protected override void PropertyInfo_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IObjectPropertyAccessProxy.ProxyValue))
            {
                NotifyOfPropertyChange(nameof(DisplayText));
                return;
            }

            base.PropertyInfo_PropertyChanged(sender, e);
        }
    }
}
