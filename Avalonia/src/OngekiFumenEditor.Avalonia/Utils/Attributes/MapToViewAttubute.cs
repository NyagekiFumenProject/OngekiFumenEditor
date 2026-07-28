namespace OngekiFumenEditor.Avalonia.Utils.Attributes
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public class MapToViewAttribute : Attribute
	{
		public Type ViewType { get; set; }
	}
}

