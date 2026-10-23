using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models;
using OngekiFumenEditor.Properties;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.ViewModels
{
    /// <summary>
    /// 删除仍有引用的变速组时，让用户选择转移引用、连同引用删除或取消。
    /// 对话框只做选择；真正的数据修改由查看器包成单个撤销动作执行。
    /// </summary>
    public sealed class SoflanGroupDeleteDialogViewModel : Screen
    {
        public SoflanGroupDeleteDialogViewModel(
            SoflanGroupNode group,
            int referenceCount,
            IEnumerable<SoflanGroupNode> alternativeGroups)
        {
            DisplayName = Resources.SoflanGroupDeleteDialogTitle;
            GroupDisplayName = SoflanGroupDisplayName.FormatGroup(group);
            Message = string.Format(Resources.SoflanGroupDeleteDialogReferences, GroupDisplayName, referenceCount);

            foreach (var option in (alternativeGroups ?? Enumerable.Empty<SoflanGroupNode>())
                .OrderBy(x => x.Id)
                .Select(SoflanGroupOption.FromGroup))
                GroupOptions.Add(option);

            SelectedAlternative = GroupOptions.FirstOrDefault();
        }

        public string GroupDisplayName { get; }
        public string Message { get; }

        public ObservableCollection<SoflanGroupOption> GroupOptions { get; } = new();

        private SoflanGroupOption selectedAlternative;
        public SoflanGroupOption SelectedAlternative
        {
            get => selectedAlternative;
            set => Set(ref selectedAlternative, value);
        }

        public bool CanTransfer => GroupOptions.Count > 0;
        public bool ShowCannotTransfer => !CanTransfer;

        public SoflanGroupDeleteChoice Choice { get; private set; } = SoflanGroupDeleteChoice.Cancel;

        public Task TransferAndDelete()
        {
            if (SelectedAlternative is null)
                return Task.CompletedTask;

            Choice = SoflanGroupDeleteChoice.Transfer;
            return TryCloseAsync(true);
        }

        public Task DeleteReferences()
        {
            Choice = SoflanGroupDeleteChoice.DeleteReferences;
            return TryCloseAsync(true);
        }

        public Task Cancel()
        {
            Choice = SoflanGroupDeleteChoice.Cancel;
            return TryCloseAsync(false);
        }
    }
}
