using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Buffers;

namespace OngekiFumenEditor.Utils
{
	public static class ArrayPoolExtensionMethod
	{
		private class AutoDisposable<T> : IDisposable
		{
			public T[] RentArray { get; set; }
			public ArrayPool<T> Pool { get; set; }

			public void Dispose()
			{
				if (RentArray is not null)
					Pool.Return(RentArray);
				RentArray = default;
				ObjectPool<AutoDisposable<T>>.Return(this);
			}
		}

		public static IDisposable RentWithUsingDisposable<T>(this ArrayPool<T> arrayPool, int minimumLength, out T[] rentArray)
		{
			var arr = arrayPool.Rent(minimumLength);
			rentArray = arr;
			var d = ObjectPool<AutoDisposable<T>>.Get();
			d.RentArray = arr;
			d.Pool = arrayPool;
			return d;
		}
	}
}
