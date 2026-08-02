using System;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr
{
	public class CommandArgs
	{
		private static readonly char[] SplitEmptyCharArray = new[] {/* ' ',*/ '\t' };

		private string line = string.Empty;
		private Dictionary<Type, Array> cacheDataArray = new Dictionary<Type, Array>();
		private readonly Dictionary<Type, IArgValueConverter> converters;

		public CommandArgs()
			: this(CreateDefaultConverters())
		{
		}

		internal static IReadOnlyList<IArgValueConverter> CreateDefaultConverters() =>
		[
			new ArgStringValueConverter(),
			new ArgSingleValueConverter(),
			new ArgDoubleValueConverter(),
			new ArgBoolValueConverter(),
			new ArgIntValueConverter(),
			new ArgLongValueConverter()
		];

		public CommandArgs(IEnumerable<IArgValueConverter> argValueConverters)
		{
			converters = argValueConverters
				.GroupBy(x => x.ConvertType)
				.ToDictionary(x => x.Key, x => x.First());
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

		public string GetRawData(int index)
		{
			return GetRawDataArray().ElementAtOrDefault(index);
		}

		public string[] GetRawDataArray()
		{
			return line.Trim().Split(SplitEmptyCharArray);
		}

		public T[] GetDataArray<T>()
		{
			var type = typeof(T);
			if (cacheDataArray.TryGetValue(type, out var array))
				return (T[])array;

			T[] arr = default;
			var inputs = line.Trim().Split(SplitEmptyCharArray);

			if (!converters.TryGetValue(type, out var argConverter))
				throw new InvalidOperationException($"No OGKR argument converter is registered for '{type.FullName}'.");

			arr = argConverter.Parser(inputs).OfType<T>().ToArray();

			cacheDataArray[type] = arr;
			return arr;
		}
	}
}


