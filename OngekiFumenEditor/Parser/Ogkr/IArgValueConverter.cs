using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr
{
	public interface IArgValueConverter
	{
		Type ConvertType { get; }
		IEnumerable Parser(IEnumerable<string> inputs);
	}

	[Export(typeof(IArgValueConverter))]
	public class ArgStringValueConverter : IArgValueConverter
	{
		public Type ConvertType => typeof(string);
		public IEnumerable Parser(IEnumerable<string> inputs) => inputs;
	}

	[Export(typeof(IArgValueConverter))]
	public class ArgSingleValueConverter : IArgValueConverter
	{
		public Type ConvertType => typeof(float);
		public IEnumerable Parser(IEnumerable<string> inputs) => inputs.Select(x => float.TryParse(x, out var d) ? d : default);
	}

	[Export(typeof(IArgValueConverter))]
	public class ArgDoubleValueConverter : IArgValueConverter
	{
		public Type ConvertType => typeof(double);
		public IEnumerable Parser(IEnumerable<string> inputs) => inputs.Select(x => double.TryParse(x, out var d) ? d : default);
	}

	[Export(typeof(IArgValueConverter))]
	public class ArgBoolValueConverter : IArgValueConverter
	{
		public Type ConvertType => typeof(bool);
		public IEnumerable Parser(IEnumerable<string> inputs) => inputs.Select(x => bool.TryParse(x, out var d) ? d : default);
	}

	[Export(typeof(IArgValueConverter))]
	public class ArgIntValueConverter : IArgValueConverter
	{
		public Type ConvertType => typeof(int);
		public IEnumerable Parser(IEnumerable<string> inputs) => inputs.Select(x => int.TryParse(x, out var d) ? d : default);
	}

	[Export(typeof(IArgValueConverter))]
	public class ArgLongValueConverter : IArgValueConverter
	{
		public Type ConvertType => typeof(long);
		public IEnumerable Parser(IEnumerable<string> inputs) => inputs.Select(x => long.TryParse(x, out var d) ? d : default);
	}
}
