using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public partial class FumenVisualEditorViewModel
    {
        private void BeginSelectionGridDrag(Point point) => InteractiveManager.BeginSelectionDrag(this, point);
        private void MoveSelectionGridDrag(Point point) => InteractiveManager.MoveSelectionDrag(this, point);
        private void EndSelectionGridDrag(bool cancel = false) => InteractiveManager.EndSelectionDrag(this, cancel);
    }
}
