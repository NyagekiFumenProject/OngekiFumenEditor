using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public abstract class CommonBatchDrawTargetBase<T> : CommonDrawTargetBase<T> where T : OngekiObjectBase
    {
        private List<T> recList = new();
        private OngekiFumen cachedFumen;

        public int DrawBatchCount { get; set; } = 20;

        public override void BeginDraw()
        {
            base.BeginDraw();
            recList.Clear();
        }

        public abstract void DrawBatch(IEnumerable<T> ongekiObjects, OngekiFumen fumen);

        private void PostBatchDraw(OngekiFumen fumen)
        {
            DrawBatch(recList, fumen);
            recList.Clear();
        }

        public override void Draw(T ongekiObject, OngekiFumen fumen)
        {
            recList.Add(ongekiObject);
            if (recList.Count >= DrawBatchCount)
                PostBatchDraw(fumen);
            cachedFumen = fumen;
        }

        public override void EndDraw()
        {
            if (recList.Count > 0)
                PostBatchDraw(cachedFumen);
            cachedFumen = default;
            base.EndDraw();
        }
    }
}
