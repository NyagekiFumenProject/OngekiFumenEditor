using System;

namespace OngekiFumenEditor.Utils
{
	public class ViewModelReferenceAttribute : Attribute
	{
		public Type ViewModelType { get; set; }
	}
}
