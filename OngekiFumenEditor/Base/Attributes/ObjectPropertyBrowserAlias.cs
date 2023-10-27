using System;

namespace OngekiFumenEditor.Base.Attributes
{
	[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
	public class ObjectPropertyBrowserAlias : Attribute
	{
		public ObjectPropertyBrowserAlias(string alias = default)
		{
			Alias = alias ?? string.Empty;
		}

		public string Alias { get; set; }
	}
}
