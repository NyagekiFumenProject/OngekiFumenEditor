namespace OngekiFumenEditor.Avalonia.Base.Attributes
{
	[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
	public class ObjectPropertyBrowserAllowSetNull : Attribute
	{
		public ObjectPropertyBrowserAllowSetNull()
		{

		}
	}
}
