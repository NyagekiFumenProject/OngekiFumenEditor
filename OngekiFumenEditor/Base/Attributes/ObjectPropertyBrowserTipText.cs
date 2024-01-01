using OngekiFumenEditor.Properties;
using System;

namespace OngekiFumenEditor.Base.Attributes
{
	[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
	public class ObjectPropertyBrowserTipText : Attribute
	{
		public ObjectPropertyBrowserTipText(string tipTextResourceKey = default)
		{
			var tipText = Resources.ResourceManager.GetString(tipTextResourceKey);
			TipText = tipText ?? string.Empty;
		}

		public string TipText { get; set; }
	}
}
