using System;

namespace OngekiFumenEditor.Base.Attributes
{
	/// <summary>
	/// 只允许单个物件被选择时显示此属性
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
	public class ObjectPropertyBrowserSingleSelectedOnly : Attribute
	{
	}
}
