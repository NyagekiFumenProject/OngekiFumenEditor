using System;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
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
            Log.LogDebug($"Thread {Name} started.", prefix: "AbortableThread");
        }

        public void Abort(bool waitForTask = true)
        {
            Log.LogDebug($"Begin to abort thread {Name}.", prefix: "AbortableThread");
            cancellationTokenSource.Cancel();
            if (waitForTask)
                task?.Wait();
            Log.LogDebug($"Aborted thread {Name}.", prefix: "AbortableThread");
        }
    }
}
