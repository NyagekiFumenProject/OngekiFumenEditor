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
            Log.LogInfo($"Thread {Name} started.", prefix: "AbortableThread");
        }


        public void Abort()
        {
            Log.LogInfo($"Begin to abort thread {Name}.", prefix: "AbortableThread");
            cancellationTokenSource.Cancel();
            thread?.Join();
            Log.LogInfo($"Aborted thread {Name}.", prefix: "AbortableThread");
        }
    }
}
