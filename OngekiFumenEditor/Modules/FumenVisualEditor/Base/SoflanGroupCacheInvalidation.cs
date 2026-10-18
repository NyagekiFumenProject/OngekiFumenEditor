using OngekiFumenEditor.Base;
using System;
using System.ComponentModel;
using System.Threading;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base
{
    /// <summary>将连续几何编辑合并为下一次绘制前的一次组归属缓存重建。</summary>
    public sealed class SoflanGroupCacheInvalidation : IDisposable
    {
        private OngekiFumen fumen;
        private int dirty = 1;

        public void Attach(OngekiFumen value)
        {
            if (ReferenceEquals(fumen, value))
                return;
            Dispose();
            fumen = value;
            if (fumen != null)
            {
                fumen.ObjectModifiedChanged += OnModified;
                fumen.ObjectCollectionChanged += Invalidate;
            }

            Invalidate();
        }

        private void OnModified(OngekiObjectBase sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is "TGrid" or "XGrid" or "SoflanGroup" or "Unit" or "Grid" or "EndTGrid")
                Invalidate();
        }

        private void Invalidate() => Interlocked.Exchange(ref dirty, 1);

        public void RefreshIfNeeded(Action rebuild)
        {
            if (Interlocked.Exchange(ref dirty, 0) == 0)
                return;
            try
            {
                rebuild();
            }
            catch
            {
                Invalidate();
                throw;
            }
        }

        public void Dispose()
        {
            if (fumen != null)
            {
                fumen.ObjectModifiedChanged -= OnModified;
                fumen.ObjectCollectionChanged -= Invalidate;
                fumen = null;
            }
        }
    }
}
