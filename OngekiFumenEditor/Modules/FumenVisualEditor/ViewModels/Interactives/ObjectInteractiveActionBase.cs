using OngekiFumenEditor.Base;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives
{
    public abstract class ObjectInteractiveActionBase
    {
        // 先计算候选 Grid，再由调用者整组校验并写入；不触发附轨 setter。
        internal virtual bool TryAdjustGrid(OngekiObjectBase obj, ref TGrid time, ref XGrid x, FumenVisualEditorViewModel editor, bool preserveTime = false) => true;

        public abstract void OnDragStart(OngekiObjectBase obj, Point point, FumenVisualEditorViewModel editor);
        public abstract void OnDragMove(OngekiObjectBase obj, Point point, FumenVisualEditorViewModel editor);
        public abstract void OnDragEnd(OngekiObjectBase obj, Point point, FumenVisualEditorViewModel editor);

        public abstract void OnMoveCanvas(OngekiObjectBase obj, Point point, FumenVisualEditorViewModel editor);
    }
}
