using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models;
using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.ViewModels;
using OngekiFumenEditor.Properties;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels.Dialogs
{
    /// <summary>组选择对话框的树节点：与变速分组查看器保持相同的层级、颜色和 #组号。</summary>
    public sealed class SoflanGroupSelectNodeViewModel : PropertyChangedBase
    {
        public SoflanGroupSelectNodeViewModel(SoflanGroupTreeNode model)
        {
            Model = model;
            ColorBrush = model is SoflanGroupNode group
                ? SoflanGroupColorBrush.Create(group.Id)
                : Brushes.Transparent;
        }

        public SoflanGroupTreeNode Model { get; }
        public ObservableCollection<SoflanGroupSelectNodeViewModel> Children { get; } = new();

        /// <summary>是否为对话框内快速新增、尚未写入组树的节点。</summary>
        public bool IsPending { get; init; }
        public SoflanGroupCategoryNode PendingParent { get; init; }

        public bool IsGroup => Model is SoflanGroupNode;
        public bool IsCategory => !IsGroup;
        public int? GroupId => (Model as SoflanGroupNode)?.Id;

        public string DisplayName => IsGroup
            ? SoflanGroupDisplayName.FormatGroup((SoflanGroupNode)Model)
            : string.IsNullOrWhiteSpace(Model.Name) ? Resources.SoflanGroupCategoryName : Model.Name;

        public string IdLabel => GroupId is int id ? $"#{id}" : string.Empty;
        public Brush ColorBrush { get; }

        private bool isExpanded = true;
        public bool IsExpanded
        {
            get => isExpanded;
            set => Set(ref isExpanded, value);
        }

        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set => Set(ref isSelected, value);
        }
    }
}
