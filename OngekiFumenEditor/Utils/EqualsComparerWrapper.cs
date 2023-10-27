using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Utils
{
	public class EqualsComparerWrapper<T> : IEqualityComparer<T>
	{
		private Func<T, T, bool> comp;

		public EqualsComparerWrapper(Func<T, T, bool> comp) => this.comp = comp;

		public bool Equals(T x, T y) => comp(x, y);

		public int GetHashCode(T obj) => obj?.GetHashCode() ?? 0;
	}
}
