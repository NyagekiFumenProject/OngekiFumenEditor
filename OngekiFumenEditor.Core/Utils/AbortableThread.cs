using System;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Core.Utils
{
    public class AbortableThread
    {
        private CancellationTokenSource cancellationTokenSource;
        private Task task;

        public CancellationToken CancellationToken => cancellationTokenSource.Token;

        public AbortableThread(Action<CancellationToken> cancellableMethod)
        {
            cancellationTokenSource = new CancellationTokenSource();
            task = new Task(() => cancellableMethod?.Invoke(CancellationToken), CancellationToken, TaskCreationOptions.LongRunning);
            Name = $"AbortableThread:{cancellableMethod}";
        }

        public string Name { get; set; }

        public void Start()
        {
            task.Start();
            CoreLog.LogDebug($"Thread {Name} started.");
        }

        public void Abort(bool waitForTask = true)
        {
            CoreLog.LogDebug($"Begin to abort thread {Name}.");
            cancellationTokenSource.Cancel();
            if (waitForTask)
                task?.Wait();
            CoreLog.LogDebug($"Aborted thread {Name}.");
        }
    }
}

