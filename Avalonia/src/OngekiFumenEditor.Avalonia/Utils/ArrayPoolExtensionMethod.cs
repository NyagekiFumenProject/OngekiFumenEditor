using System.Buffers;
using OngekiFumenEditor.Avalonia.Utils.ObjectPool;

namespace OngekiFumenEditor.Avalonia.Utils
{
	public static class ArrayPoolExtensionMethod
	{
			private class AutoDisposable<T> : IDisposable
			{
				private bool isActive;

				public T[] RentArray { get; set; }
				public ArrayPool<T> Pool { get; set; }

				public void Rent(ArrayPool<T> sourcePool, T[] array)
				{
					Pool = sourcePool;
					RentArray = array;
					isActive = true;
				}

				public void Dispose()
				{
					if (!isActive)
						return;

					isActive = false;
					var arr = RentArray;
					var pool = Pool;
					RentArray = default;
					Pool = default;
					if (arr is not null)
						pool.Return(arr);
					ObjectPool<AutoDisposable<T>>.Return(this);
				}
			}

			public static IDisposable RentWithUsingDisposable<T>(this ArrayPool<T> arrayPool, int minimumLength, out T[] rentArray)
			{
				var arr = arrayPool.Rent(minimumLength);
				rentArray = arr;
				var d = ObjectPool<AutoDisposable<T>>.Get();
				d.Rent(arrayPool, arr);
				return d;
			}
	}
}

