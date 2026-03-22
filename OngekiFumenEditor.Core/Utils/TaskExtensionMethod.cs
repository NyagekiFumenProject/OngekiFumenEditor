using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    public static class TaskExtensionMethod
    {
        public static async Task<T> WithTimeout<T>(this Task<T> t, int timeoutMsec, CancellationToken cancellationToken = default)
        {
            var dt = Task.Delay(timeoutMsec, cancellationToken).ContinueWith((a, b) => default(T), cancellationToken);
            var task = await Task.WhenAny(dt, t);

            return task == t ? await t : default(T);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NoWait(this Task t)
        {

        }
    }
}
