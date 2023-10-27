using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Utils
{
	public class ComparerWrapper<T> : IComparer<T>
	{
		private Func<T, T, int> comp;

		public ComparerWrapper(Func<T, T, int> comp) => this.comp = comp;

		public int Compare(T x, T y)
		{
			return comp(x, y);
		}
	}
}
