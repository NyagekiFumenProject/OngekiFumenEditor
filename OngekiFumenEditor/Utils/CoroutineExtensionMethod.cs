using kRpc.Coroutines;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    public static class CoroutineExtensionMethod
    {
        public static async ValueTask<T> StartValueTask<T>(this IEnumerator<IWaitable<T>> enumerator)
        {
            return await Task.Run(async () =>
            {
                var co = CoroutineMgr.Instance.StartCoroutine(enumerator);
                while (!co.IsDone)
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                return co.Return.GetValue();
            });
        }

        public static async ValueTask StartValueTask(this IEnumerator<IWaitable> enumerator)
        {
            await Task.Run(async () =>
            {
                var co = CoroutineMgr.Instance.StartCoroutine(enumerator);
                while (!co.IsDone)
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
            });
        }
    }
}
