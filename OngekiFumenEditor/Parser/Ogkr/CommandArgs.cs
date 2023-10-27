using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr
{
	public class CommandArgs
	{
		private static readonly char[] SplitEmptyCharArray = new[] {/* ' ',*/ '\t' };

		private string line = string.Empty;
		private Dictionary<Type, Array> cacheDataArray = new Dictionary<Type, Array>();
		private Dictionary<Type, IArgValueConverter> converters = new Dictionary<Type, IArgValueConverter>();

		public CommandArgs()
		{
			converters = IoC.GetAll<IArgValueConverter>().ToDictionary(x => x.ConvertType);
		}

		public string Line
		{
			get
			{
				return line;
			}
			set
			{
				cacheDataArray.Clear();
				line = value;
			}
		}

		public T GetData<T>(int index)
		{
			return GetDataArray<T>().ElementAtOrDefault(index);
		}

		public T[] GetDataArray<T>()
		{
			var type = typeof(T);
			if (cacheDataArray.TryGetValue(type, out var array))
				return (T[])array;

			T[] arr = default;
			var inputs = line.Trim().Split(SplitEmptyCharArray);

			if (converters.TryGetValue(type, out var argConverter))
			{
				arr = argConverter.Parser(inputs).OfType<T>().ToArray();
			}
			else
			{
				var converter = TypeDescriptor.GetConverter(type);
				arr = inputs.Select(x =>
				{
					if (converter.IsValid(x))
						return (T)converter.ConvertFromString(x);
					return default;
				}).ToArray();
			}

			cacheDataArray[type] = arr;
			return arr;
		}
	}
}
