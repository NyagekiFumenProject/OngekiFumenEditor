using System;
using System.Threading;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public partial class FumenVisualEditorViewModel
    {
        // TODO: Replace this lock-based bridge with immutable per-frame render snapshots.
        private readonly ReaderWriterLockSlim renderDataLock = new(LockRecursionPolicy.SupportsRecursion);

        public IDisposable EnterRenderDataReadLock()
        {
            renderDataLock.EnterReadLock();
            return new RenderDataLockScope(renderDataLock, false);
        }

        public IDisposable EnterRenderDataWriteLock()
        {
            renderDataLock.EnterWriteLock();
            return new RenderDataLockScope(renderDataLock, true);
        }

        private sealed class RenderDataLockScope : IDisposable
        {
            private readonly ReaderWriterLockSlim renderDataLock;
            private readonly bool writeLock;
            private bool disposed;

            public RenderDataLockScope(ReaderWriterLockSlim renderDataLock, bool writeLock)
            {
                this.renderDataLock = renderDataLock;
                this.writeLock = writeLock;
            }

            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;
                if (writeLock)
                    renderDataLock.ExitWriteLock();
                else
                    renderDataLock.ExitReadLock();
            }
        }
    }
}
