using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    public class AbortableThread
    {
        private Thread thread;
        private CancellationTokenSource cancellationTokenSource;

        public AbortableThread(Action<CancellationToken> cancellableMethod)
        {
            cancellationTokenSource = new CancellationTokenSource();
            thread = new Thread(() => cancellableMethod?.Invoke(cancellationTokenSource.Token));
            Name = $"AbortableThread:{cancellableMethod}";
        }

        public bool IsBackground
        {
            get
            {
                return thread.IsBackground;
            }

            set
            {
                thread.IsBackground = value;
            }
        }

        public string Name
        {
            get
            {
                return thread.Name;
            }
            set
            {
                thread.Name = value;
            }
        }

        public void Start()
        {
            thread.Start();
            Log.LogDebug($"Thread {Name} started.", prefix: "AbortableThread");
        }


        public void Abort(bool waitForTask = true)
        {
            Log.LogDebug($"Begin to abort thread {Name}.", prefix: "AbortableThread");
            cancellationTokenSource.Cancel();
            if (waitForTask)
                thread?.Join();
            Log.LogDebug($"Aborted thread {Name}.", prefix: "AbortableThread");
        }
    }
}
