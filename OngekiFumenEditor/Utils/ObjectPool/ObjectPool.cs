using Caliburn.Micro;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Utils.ObjectPool
{
	public class ObjectPool<T> : ObjectPoolBase where T : new()
	{
		#region AutoImpl

		private static ObjectPool<T> instance;
		private static ObjectPool<T> Instance
		{
			get
			{
				if (instance == null)
				{
					instance = new ObjectPool<T>();
					IoC.Get<ObjectPoolManager>().RegisterNewObjectPool(instance);
				}

				return instance;
			}
		}

		#endregion

		private ConcurrentBag<T> cache_obj = new ConcurrentBag<T>();

		public static bool EnableTrim { get; set; } = true;
		public override int CachingObjectCount => cache_obj.Count;

		protected override void OnReduceObjects()
		{
			if (!EnableTrim)
				return;
			var cachingObjectCount = CachingObjectCount;

			var count = cachingObjectCount > MaxTempCache ?
				(MaxTempCache + ((cachingObjectCount - MaxTempCache) / 2)) :
				cachingObjectCount / 4; ;

			for (int i = 0; i < count / 2; i++)
				if (!cache_obj.TryTake(out _))
					break;
		}

		#region Sugar~

		private class AutoDisposable : IDisposable
		{
			public T RefObject { get; set; }

			public void Dispose()
			{
				if (RefObject is not null)
					Return(RefObject);
				RefObject = default;
				ObjectPool<AutoDisposable>.Return(this);
			}
		}

		public static IDisposable GetWithUsingDisposable(out T obj, out bool isNewObject)
		{
			isNewObject = Get(out obj);
			var d = ObjectPool<AutoDisposable>.Get();
			d.RefObject = obj;
			return d;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="obj">gained object</param>
		/// <returns>it's a new object if returns true</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Get(out T obj) => Instance.GetInternal(out obj);

		private bool GetInternal(out T obj)
		{
			var isSuccess = cache_obj.TryTake(out obj);
			if (isSuccess)
				(obj as ICacheCleanable)?.OnBeforeGetClean();
			else
				obj = new T();

			return !isSuccess;
		}

		public static T Get()
		{
			Get(out var t);
			return t;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Return(T obj) => Instance.ReturnInternal(obj);

		private void ReturnInternal(T obj)
		{
			if (obj == null)
				return;

			cache_obj.Add(obj);
			(obj as ICacheCleanable)?.OnAfterPutClean();
		}

		#endregion
	}
}
