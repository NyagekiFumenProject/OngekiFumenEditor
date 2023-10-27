using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
	public static class TaskExtensionMethod
	{
		/// <summary>
		/// 对一个Task钦定一个timeout，超时就不管了
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="t"></param>
		/// <param name="timeoutMsec"></param>
		/// <returns></returns>
		public static async Task<T> WithTimeout<T>(this Task<T> t, int timeoutMsec, CancellationToken cancellationToken = default)
		{
			var dt = Task.Delay(timeoutMsec, cancellationToken).ContinueWith((a, b) => default(T), cancellationToken);
			var task = await Task.WhenAny(dt, t);

			return task == t ? await t : default(T);
		}
	}
}
