using Injectio.Attributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr
{
	public interface IArgValueConverter
	{
		Type ConvertType { get; }
		IEnumerable Parser(IEnumerable<string> inputs);
	}

	[RegisterSingleton<IArgValueConverter>]
	public class ArgStringValueConverter : IArgValueConverter
	{
		public Type ConvertType => typeof(string);
		public IEnumerable Parser(IEnumerable<string> inputs) => inputs;
	}

	[RegisterSingleton<IArgValueConverter>]
	public class ArgSingleValueConverter : IArgValueConverter
	{
		public Type ConvertType => typeof(float);
		public IEnumerable Parser(IEnumerable<string> inputs) => inputs.Select(x => float.TryParse(x, out var d) ? d : default);
	}

	[RegisterSingleton<IArgValueConverter>]
	public class ArgDoubleValueConverter : IArgValueConverter
	{
		public Type ConvertType => typeof(double);
		public IEnumerable Parser(IEnumerable<string> inputs) => inputs.Select(x => double.TryParse(x, out var d) ? d : default);
	}

	[RegisterSingleton<IArgValueConverter>]
	public class ArgBoolValueConverter : IArgValueConverter
	{
		public Type ConvertType => typeof(bool);
		public IEnumerable Parser(IEnumerable<string> inputs) => inputs.Select(x => bool.TryParse(x, out var d) ? d : default);
	}

	[RegisterSingleton<IArgValueConverter>]
	public class ArgIntValueConverter : IArgValueConverter
	{
		public Type ConvertType => typeof(int);
		public IEnumerable Parser(IEnumerable<string> inputs) => inputs.Select(x => int.TryParse(x, out var d) ? d : default);
	}

	[RegisterSingleton<IArgValueConverter>]
	public class ArgLongValueConverter : IArgValueConverter
	{
		public Type ConvertType => typeof(long);
		public IEnumerable Parser(IEnumerable<string> inputs) => inputs.Select(x => long.TryParse(x, out var d) ? d : default);
	}
}


