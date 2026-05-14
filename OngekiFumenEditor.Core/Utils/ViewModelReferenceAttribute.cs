using System;

namespace OngekiFumenEditor.Core.Utils
{
	public class ViewModelReferenceAttribute : Attribute
	{
		public Type ViewModelType { get; set; }
	}
}

