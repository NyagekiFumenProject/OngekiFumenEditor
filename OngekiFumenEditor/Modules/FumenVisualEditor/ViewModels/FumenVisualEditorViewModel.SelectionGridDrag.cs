using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public partial class FumenVisualEditorViewModel
    {
        private void BeginSelectionGridDrag(Point point) => InteractiveManager.BeginSelectionDrag(this, point);
        private void UpdateSelectionDrag(Point point) => InteractiveManager.UpdateSelectionDrag(this, point);
        private void CommitSelectionDrag() => InteractiveManager.CommitSelectionDrag(this);

        /// <summary>
        /// 中止当前按 Grid 的物件拖动事务并重置拖动输入状态；无事务时同样幂等。
        /// </summary>
        public void AbortSelectionDrag()
        {
            InteractiveManager.AbortSelectionDrag(this);
            isObjectDragStarted = false;
            autoScrollRedispatchToken = int.MaxValue;
        }
    }
}
