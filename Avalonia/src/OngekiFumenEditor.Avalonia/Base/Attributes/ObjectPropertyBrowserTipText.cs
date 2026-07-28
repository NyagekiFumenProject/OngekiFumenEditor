using SimpleTypedLocalizer;

namespace OngekiFumenEditor.Avalonia.Base.Attributes
{
	[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
	public class ObjectPropertyBrowserTipText : Attribute
	{
		public ObjectPropertyBrowserTipText(string tipTextResourceKey = default)
		{
			var tipText = LocalizerManager.GetLocalizedStringGlobally(tipTextResourceKey);
			TipText = tipText ?? string.Empty;
		}

		public string TipText { get; set; }
	}
}

